using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class RuntimeMappingHelperLowerer
{
    public static TypeMapperModel Lower(
        TypeMapperModel model,
        CSharpCompilation compilation,
        CSharpParseOptions? parseOptions,
        CancellationToken cancellationToken)
    {
        var text = TypeMapperEmitter.EmitTransferProbe(model);
        if (text.ToString().IndexOf("_ = context.Mapper.Map<", StringComparison.Ordinal) < 0) return model;
        var tree = CSharpSyntaxTree.ParseText(text, parseOptions, cancellationToken: cancellationToken);
        var methods = tree.GetRoot(cancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => method.ExplicitInterfaceSpecifier is null)
            .ToDictionary(method => method.Identifier.ValueText, StringComparer.Ordinal);
        var candidates = new Dictionary<int, List<(MethodDeclarationSyntax Method, IfStatementSyntax Guard)>>();
        for (var index = 0; index < model.Mappings.Length; index++)
        {
            foreach (var name in new[] { model.Mappings[index].CreateImplMethodName, model.Mappings[index].UpdateImplMethodName })
            {
                if (name is null || !methods.TryGetValue(name, out var method)) continue;
                foreach (var guard in method.DescendantNodes(node => node is not
                             (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)).OfType<IfStatementSyntax>())
                {
                    if (!TryGetUpdate(guard, out _, out _, out _, out _)) continue;
                    if (!candidates.TryGetValue(index, out var list)) candidates.Add(index, list = []);
                    list.Add((method, guard));
                }
            }
        }

        if (candidates.Count == 0) return model;

        var semanticModel = compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);
        var mappings = model.Mappings.ToArray();
        foreach (var entry in candidates)
        {
            var changes = new List<TextChange>();
            foreach (var (method, guard) in entry.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryBuildCall(guard, method, semanticModel, text, cancellationToken) is { } call)
                    changes.Add(new TextChange(guard.Span, call));
            }
            if (changes.Count == 0) continue;

            mappings[entry.Key] = mappings[entry.Key] with
            {
                SharedConstructionMethodDeclarations = new[]
                    { mappings[entry.Key].CreateImplMethodName, mappings[entry.Key].UpdateImplMethodName }
                    .Where(name => name is not null && methods.ContainsKey(name))
                    .Select(name =>
                    {
                        var method = methods[name!];
                        return Unindent(text.GetSubText(method.Span).WithChanges(changes
                            .Where(change => method.Span.Contains(change.Span))
                            .Select(change => new TextChange(new TextSpan(change.Span.Start - method.SpanStart,
                                change.Span.Length), change.NewText!))).ToString(), Column(text, method.SpanStart));
                    }).ToImmutableArray()
            };
        }

        if (!mappings.Where((mapping, index) => mapping != model.Mappings[index]).Any()) return model;

        var lowered = model with { Mappings = mappings.ToImmutableArray() };
        var loweredTree = CSharpSyntaxTree.ParseText(TypeMapperEmitter.EmitTransferProbe(lowered), parseOptions,
            cancellationToken: cancellationToken);
        var loweredSemanticModel = compilation.AddSyntaxTrees(loweredTree).GetSemanticModel(loweredTree);
        var originalDiagnostics = Diagnostics(semanticModel, cancellationToken).GroupBy(diagnostic => diagnostic)
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (var group in Diagnostics(loweredSemanticModel, cancellationToken).GroupBy(diagnostic => diagnostic))
        {
            // A lambda can lose nullable flow or be unable to capture a ref
            // local. Retain the original scope when moving it adds diagnostics.
            if (!originalDiagnostics.TryGetValue(group.Key, out var count) || group.Count() > count)
                mappings[group.Key.Index] = model.Mappings[group.Key.Index];
        }
        return model with { Mappings = mappings.ToImmutableArray() };
    }

    private static string? TryBuildCall(
        IfStatementSyntax guard,
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        SourceText text,
        CancellationToken cancellationToken)
    {
        if (!TryGetUpdate(guard, out var target, out var invocation, out var source, out var conversion) ||
            semanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation call ||
            call.TargetMethod.ContainingType.ToDisplayString() != "Morphant.IMapper" ||
            call.TargetMethod.TypeArguments.Length != 2 ||
            semanticModel.GetTypeInfo(target, cancellationToken).Type is not { IsReferenceType: true } targetType ||
            targetType.TypeKind == TypeKind.Dynamic ||
            (conversion is null
                ? !SymbolEqualityComparer.Default.Equals(targetType, call.TargetMethod.TypeArguments[1])
                : !IsDestinationCheck(conversion, call.TargetMethod.TypeArguments, semanticModel, cancellationToken)))
            return null;

        var flow = semanticModel.AnalyzeDataFlow(source);
        if (flow is not { Succeeded: true } || source.DescendantTokens()
                .Any(token => UserExpressionLayout.HasLineBreak(token.Text))) return null;
        var identifiers = source.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
            .Select(node => (Node: node, Symbol: semanticModel.GetSymbolInfo(node, cancellationToken).Symbol)).ToArray();
        var inputs = identifiers.Select(item => item.Symbol)
            .Where(symbol => symbol is ILocalSymbol or IParameterSymbol &&
                symbol is not ILocalSymbol { IsConst: true } &&
                !symbol.DeclaringSyntaxReferences.Any(reference => source.Span.Contains(reference.Span)))
            .Distinct(SymbolEqualityComparer.Default).ToArray();
        var capture = source.DescendantNodesAndSelf().Any(node => node is ThisExpressionSyntax or BaseExpressionSyntax) ||
            inputs.Any(symbol => flow.WrittenInside.Contains(symbol, SymbolEqualityComparer.Default) ||
                flow.CapturedInside.Contains(symbol, SymbolEqualityComparer.Default) ||
                flow.CapturedOutside.Contains(symbol, SymbolEqualityComparer.Default) ||
                TypeOf(symbol!) is not { } type ||
                !type.IsReferenceType && type.SpecialType == SpecialType.None && type.TypeKind != TypeKind.Enum &&
                    type is not INamedTypeSymbol { IsReadOnly: true }) ||
            identifiers.Any(item => item.Symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction });

        var expression = text.ToString(source.Span);
        var state = (string?)null;
        string selector;
        if (capture || inputs.Length == 0)
        {
            selector = (capture ? string.Empty : "static ") + "() => " + expression;
        }
        else
        {
            var usedNames = new HashSet<string>(method.DescendantTokens()
                .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
                .Select(token => token.ValueText), StringComparer.Ordinal);
            var parameter = UserResultMappingPlanner.AllocateName(inputs.Length == 1 ? "s" : "state", usedNames);
            state = inputs.Length == 1 ? Identifier(inputs[0]!.Name) :
                "(" + string.Join(", ", inputs.Select(symbol => Identifier(symbol!.Name))) + ")";
            var replacements = identifiers.Where(item => inputs.Contains(item.Symbol, SymbolEqualityComparer.Default))
                .Select(item => new TextChange(new TextSpan(item.Node.SpanStart - source.SpanStart, item.Node.Span.Length),
                    parameter + (inputs.Length == 1 ? string.Empty : "." + Identifier(item.Symbol!.Name))));
            expression = text.GetSubText(source.Span).WithChanges(replacements).ToString();
            selector = "static " + parameter + " => " + expression;
        }

        var indent = new string(' ', Column(text, guard.SpanStart));
        var typeArguments = string.Empty;
        if (conversion is not null || !SymbolEqualityComparer.Default.Equals(
                semanticModel.GetTypeInfo(source, cancellationToken).Type, call.TargetMethod.TypeArguments[0]))
        {
            var types = new List<string>();
            if (state is not null)
                types.Add(inputs.Length == 1 ? TypeName(TypeOf(inputs[0]!)!) :
                    "(" + string.Join(", ", inputs.Select(symbol => TypeName(TypeOf(symbol!)!) + " " + Identifier(symbol!.Name))) + ")");
            types.AddRange(call.TargetMethod.TypeArguments.Select(TypeName));
            typeArguments = "<\r\n" + string.Join(",\r\n", types.Select(type => indent + "    " + type)) + ">";
        }
        var expressionIndent = Column(text, source.Ancestors().OfType<StatementSyntax>().First().SpanStart);
        var arguments = new List<string> { text.ToString(target.Span) };
        if (state is not null) arguments.Add(state);
        arguments.Add(selector);
        arguments.Add("context");
        return "global::Morphant.GeneratedCode.MappingHelpers.UpdateInPlace" + typeArguments + "(\r\n" +
            string.Join(",\r\n", arguments.Select(argument => indent + "    " +
                Unindent(argument, expressionIndent).Replace("\r\n", "\r\n" + indent + "    "))) + ");";
    }

    private static bool TryGetUpdate(IfStatementSyntax guard, out ExpressionSyntax target,
        out InvocationExpressionSyntax invocation, out ExpressionSyntax source, out SwitchExpressionSyntax? conversion)
    {
        target = source = null!;
        invocation = null!;
        conversion = null;
        if (guard.Else is not null || guard.Condition is not IsPatternExpressionSyntax
            { Expression: MemberAccessExpressionSyntax member, Pattern: RecursivePatternSyntax
                { Type: null, PropertyPatternClause: { Subpatterns.Count: 0 },
                    Designation: SingleVariableDesignationSyntax designation } } ||
            guard.Statement is not BlockSyntax body || body.Statements.Count is < 1 or > 3 ||
            body.Statements.Last() is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax
                { Left: IdentifierNameSyntax { Identifier.ValueText: "_" }, Right: InvocationExpressionSyntax map } } ||
            map.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax
                { Identifier.ValueText: "Map", TypeArgumentList.Arguments.Count: 2 },
                Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Mapper",
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "context" } } } ||
            map.ArgumentList.Arguments.Count != 2 ||
            map.ArgumentList.Arguments[0].NameColon?.Name.Identifier.ValueText is not (null or "source") ||
            map.ArgumentList.Arguments[1].NameColon?.Name.Identifier.ValueText != "destination" ||
            map.ArgumentList.Arguments[1].Expression is not IdentifierNameSyntax destination)
            return false;

        var precedingStatements = body.Statements.Count - 1;
        if (destination.Identifier.ValueText != designation.Identifier.ValueText)
        {
            if (precedingStatements == 0 ||
                !TryGetInitializer(body.Statements[precedingStatements - 1], destination.Identifier.ValueText,
                    out var initializer) ||
                initializer is not SwitchExpressionSyntax
                { GoverningExpression: IdentifierNameSyntax checkedDestination } check ||
                checkedDestination.Identifier.ValueText != designation.Identifier.ValueText)
                return false;
            conversion = check;
            precedingStatements--;
        }

        source = map.ArgumentList.Arguments[0].Expression;
        if (precedingStatements == 1)
        {
            if (source is not IdentifierNameSyntax value ||
                !TryGetInitializer(body.Statements[0], value.Identifier.ValueText, out source))
                return false;
        }
        else if (precedingStatements != 0) return false;
        target = member;
        invocation = map;
        return true;
    }

    private static bool TryGetInitializer(StatementSyntax statement, string name, out ExpressionSyntax value)
    {
        value = null!;
        if (statement is not LocalDeclarationStatementSyntax
            { Declaration.Variables: { Count: 1 } variables } ||
            variables[0].Identifier.ValueText != name || variables[0].Initializer is not { } initializer)
            return false;
        value = initializer.Value;
        return true;
    }

    private static bool IsDestinationCheck(SwitchExpressionSyntax check,
        ImmutableArray<ITypeSymbol> mappingTypes, SemanticModel semanticModel, CancellationToken token)
    {
        if (check.Arms.Count != 2 || check.Arms.Any(arm => arm.WhenClause is not null) ||
            check.Arms[0] is not
            { Pattern: DeclarationPatternSyntax { Type: var type,
                Designation: SingleVariableDesignationSyntax compatible }, Expression: IdentifierNameSyntax value } ||
            compatible.Identifier.ValueText != value.Identifier.ValueText ||
            !SymbolEqualityComparer.Default.Equals(semanticModel.GetTypeInfo(type, token).Type, mappingTypes[1]) ||
            check.Arms[1] is not
            { Pattern: VarPatternSyntax { Designation: SingleVariableDesignationSyntax incompatible },
                Expression: ThrowExpressionSyntax { Expression: InvocationExpressionSyntax failure } } ||
            semanticModel.GetOperation(failure, token) is not IInvocationOperation call ||
            call.TargetMethod.ContainingType.ToDisplayString() != "Morphant.Exceptions.NestedDestinationTypeMismatchException" ||
            call.TargetMethod.Name != "Create" ||
            !Enumerable.SequenceEqual<ITypeSymbol>(call.TargetMethod.TypeArguments, mappingTypes, SymbolEqualityComparer.Default) ||
            failure.ArgumentList.Arguments.Count != 2 ||
            semanticModel.GetSymbolInfo(failure.ArgumentList.Arguments[0].Expression, token).Symbol is not
                IFieldSymbol { Name: "Update", ContainingType: { } operationType } ||
            operationType.ToDisplayString() != "Morphant.Context.MappingOperation" ||
            failure.ArgumentList.Arguments[1].Expression is not IdentifierNameSyntax actual ||
            actual.Identifier.ValueText != incompatible.Identifier.ValueText)
            return false;
        return true;
    }

    private static ITypeSymbol? TypeOf(ISymbol symbol) => symbol switch
    {
        ILocalSymbol local => local.Type,
        IParameterSymbol parameter => parameter.Type,
        _ => null
    };

    private static string TypeName(ITypeSymbol type) => TypeMapperMappingTypePolicy.GetGeneratedTypeName(type);

    private static string Identifier(string name) => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ||
        SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    private static int Column(SourceText text, int position) => position - text.Lines.GetLineFromPosition(position).Start;

    private static string Unindent(string value, int indent)
    {
        var lines = value.Replace("\r\n", "\n").Split('\n');
        for (var index = 1; index < lines.Length; index++)
        {
            var count = 0;
            while (count < indent && count < lines[index].Length && lines[index][count] == ' ') count++;
            lines[index] = lines[index].Substring(count);
        }
        return string.Join("\r\n", lines);
    }

    private static IEnumerable<(int Index, string Key)> Diagnostics(SemanticModel model, CancellationToken token) =>
        model.GetDiagnostics(cancellationToken: token)
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
            .Select(diagnostic => TypeMapperEmitter.TryGetTransferProbeMappingIndex(diagnostic, out var index)
                ? (Index: index, Key: diagnostic.Id + ":" + diagnostic.GetMessage()) : (Index: -1, Key: string.Empty))
            .Where(diagnostic => diagnostic.Index >= 0);
}
