using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class SharedConstructionLowerer
{
    public static TypeMapperModel Lower(
        TypeMapperModel model,
        CSharpCompilation compilation,
        CSharpParseOptions? parseOptions,
        CancellationToken cancellationToken)
    {
        if (!model.Mappings.Any(mapping => mapping.ControlFlow is not null))
        {
            return model;
        }

        var tree = CSharpSyntaxTree.ParseText(
            TypeMapperEmitter.EmitTransferProbe(model),
            parseOptions,
            cancellationToken: cancellationToken);
        var root = tree.GetRoot(cancellationToken);
        var text = tree.GetText(cancellationToken);
        var semanticModel = compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => method.ExplicitInterfaceSpecifier is null)
            .ToDictionary(method => method.Identifier.ValueText, StringComparer.Ordinal);
        var names = new HashSet<string>(methods.Keys, StringComparer.Ordinal);
        foreach (var mapping in model.Mappings)
        {
            for (var type = mapping.AnalysisContext.TargetMapper; type is not null; type = type.BaseType)
            {
                names.UnionWith(type.GetMembers().Select(member => member.Name));
            }
        }

        var mappings = model.Mappings.ToArray();
        for (var index = 0; index < mappings.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mapping = mappings[index];
            if (mapping.ControlFlow is null)
            {
                continue;
            }

            var implementations = new[] { mapping.CreateImplMethodName, mapping.UpdateImplMethodName }
                .Where(name => name is not null && methods.ContainsKey(name))
                .Select(name => methods[name!]).ToArray();
            var candidates = implementations.SelectMany(method => FindCandidates(method, mapping, text))
                .GroupBy(candidate => candidate.Body, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .SelectMany(group => group)
                .Select(candidate => Bind(candidate, mapping, semanticModel, cancellationToken))
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!)
                .GroupBy(candidate => candidate.Key, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderByDescending(group => group.First().Syntax.Span.Length);
            var changes = new List<TextChange>();
            var helpers = new List<string>();

            foreach (var group in candidates)
            {
                var available = group.Where(candidate => !changes.Any(change =>
                    change.Span.OverlapsWith(candidate.Syntax.Span))).ToArray();
                if (available.Length < 2)
                {
                    continue;
                }

                var existing = available.FirstOrDefault(candidate =>
                    candidate.Syntax.Method.Identifier.ValueText == mapping.CreateImplMethodName &&
                    candidate.Syntax.First == candidate.Syntax.Method.Body!.Statements[0] &&
                    candidate.Parameters.All(parameter => parameter.Symbol is IParameterSymbol));
                var template = existing ?? available[0];
                string name;
                ImmutableArray<Parameter> parameters;
                if (existing is not null)
                {
                    name = mapping.CreateImplMethodName!;
                    parameters = existing.Syntax.Method.ParameterList.Parameters.Select(parameter =>
                    {
                        var symbol = (IParameterSymbol)semanticModel.GetDeclaredSymbol(parameter, cancellationToken)!;
                        return new Parameter(symbol, parameter.Type!.ToString(), parameter.Identifier.Text);
                    }).ToImmutableArray();
                }
                else
                {
                    name = UserResultMappingPlanner.AllocateName("__Construct", names);
                    parameters = template.Parameters;
                    var writer = new CodeWriter();
                    writer.Line("private " + (mapping.RequiresUnsafeContext ? "unsafe " : string.Empty) +
                        mapping.DestinationTypeName + " " + name + "(");
                    writer.Indent();
                    for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                    {
                        var parameter = parameters[parameterIndex];
                        writer.Line((parameter.ByReference ? "ref " : string.Empty) + parameter.TypeName + " " + parameter.Name +
                            (parameterIndex == parameters.Length - 1 ? ")" : ","));
                    }
                    writer.Unindent();
                    writer.Line("{");
                    writer.Indent();
                    writer.Line(template.Syntax.Body);
                    writer.Unindent();
                    writer.Line("}");
                    helpers.Add(writer.ToString().TrimEnd());
                }

                foreach (var candidate in available)
                {
                    if (ReferenceEquals(candidate, existing))
                    {
                        continue;
                    }

                    var arguments = parameters.Select(parameter =>
                        parameter.Name == "operation" &&
                        candidate.Parameters.All(value => value.Name != "operation")
                            ? "global::Morphant.Context.MappingOperation.Update"
                            : (parameter.ByReference ? "ref " : string.Empty) + parameter.Name);
                    var call = "return " + name + "(" + string.Join(", ", arguments) + ");";
                    var span = candidate.Syntax.Span;
                    if (candidate.Syntax.First.Parent is BlockSyntax { Parent: ElseClauseSyntax @else } block &&
                        candidate.Syntax.First == block.Statements[0] &&
                        @else.Parent is IfStatementSyntax @if &&
                        semanticModel.AnalyzeControlFlow(@if.Statement) is { EndPointIsReachable: false })
                    {
                        // Extraction removed the declarations that required this
                        // else scope. Its call can now be the continuation.
                        span = TextSpan.FromBounds(@if.Statement.Span.End, @else.Span.End);
                        call = "\r\n\r\n" + new string(' ', Column(text, @if.SpanStart)) + call;
                    }
                    changes.Add(new TextChange(span, call));
                }
            }

            if (changes.Count == 0)
            {
                continue;
            }

            var declarations = implementations.Select(method =>
            {
                var methodText = text.GetSubText(method.Span);
                var methodChanges = changes.Where(change => method.Span.Contains(change.Span))
                    .Select(change => new TextChange(
                        new TextSpan(change.Span.Start - method.Span.Start, change.Span.Length), change.NewText!));
                return Unindent(methodText.WithChanges(methodChanges).ToString(), Column(text, method.SpanStart));
            }).Concat(helpers).ToImmutableArray();
            mappings[index] = mapping with { SharedConstructionMethodDeclarations = declarations };
        }

        var shared = model with { Mappings = mappings.ToImmutableArray() };
        if (!mappings.Where((mapping, index) =>
                !mapping.SharedConstructionMethodDeclarations.IsDefaultOrEmpty &&
                model.Mappings[index].SharedConstructionMethodDeclarations.IsDefaultOrEmpty).Any())
        {
            return model;
        }

        // Nullable member flow and ref escape rules can depend on the caller's
        // scope. Keep that scope when extracting a branch changes diagnostics.
        var sharedTree = CSharpSyntaxTree.ParseText(
            TypeMapperEmitter.EmitTransferProbe(shared), parseOptions,
            cancellationToken: cancellationToken);
        var sharedSemanticModel = compilation.AddSyntaxTrees(sharedTree).GetSemanticModel(sharedTree);
        var originalDiagnostics = Diagnostics(semanticModel, cancellationToken)
            .GroupBy(diagnostic => diagnostic)
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (var group in Diagnostics(sharedSemanticModel, cancellationToken).GroupBy(diagnostic => diagnostic))
        {
            if (!originalDiagnostics.TryGetValue(group.Key, out var count) || group.Count() > count)
            {
                mappings[group.Key.Index] = model.Mappings[group.Key.Index];
            }
        }

        return model with { Mappings = mappings.ToImmutableArray() };
    }

    private static IEnumerable<(int Index, string Key)> Diagnostics(
        SemanticModel semanticModel,
        CancellationToken cancellationToken) =>
        semanticModel.GetDiagnostics(cancellationToken: cancellationToken)
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
            .Select(diagnostic => TypeMapperEmitter.TryGetTransferProbeMappingIndex(diagnostic, out var index)
                ? (Index: index, Key: diagnostic.Id + ":" + diagnostic.GetMessage())
                : (Index: -1, Key: string.Empty))
            .Where(diagnostic => diagnostic.Index >= 0);

    private static IEnumerable<Candidate> FindCandidates(
        MethodDeclarationSyntax method,
        TypeMapperMappingModel mapping,
        SourceText text)
    {
        if (method.Body is null)
        {
            yield break;
        }

        foreach (var block in method.Body.DescendantNodesAndSelf(node => node is not
                     (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)).OfType<BlockSyntax>())
        {
            var statements = block.Statements;
            for (var index = 0; index < statements.Count; index++)
            {
                if (!statements.Skip(index).Any(statement => statement.DescendantNodesAndSelf(node => node is not
                        (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
                    .OfType<ObjectCreationExpressionSyntax>().Any(creation =>
                        creation.Type.ToString() == mapping.NonNullDestinationTypeName)))
                {
                    continue;
                }

                // A shared branch must terminate its caller, not just a nested
                // block or loop. Generated selection paths end in return/throw.
                if (statements.Last() is not (ReturnStatementSyntax or ThrowStatementSyntax or
                    IfStatementSyntax or SwitchStatementSyntax))
                {
                    continue;
                }

                var span = TextSpan.FromBounds(statements[index].SpanStart, statements.Last().Span.End);
                yield return new Candidate(method, statements[index], statements.Last(), span,
                    Unindent(text.ToString(span), Column(text, span.Start)));
            }
        }
    }

    private static BoundCandidate? Bind(
        Candidate candidate,
        TypeMapperMappingModel mapping,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var flow = semanticModel.AnalyzeDataFlow(candidate.First, candidate.Last);
        if (flow is not { Succeeded: true } ||
            semanticModel.AnalyzeControlFlow(candidate.First, candidate.Last) is not
                { Succeeded: true, EndPointIsReachable: false })
        {
            return null;
        }

        var identifiers = candidate.First.Parent!.DescendantNodes()
            .OfType<IdentifierNameSyntax>().Where(identifier => candidate.Span.Contains(identifier.Span) &&
                identifier.Parent is not (NameColonSyntax or NameEqualsSyntax)).ToArray();
        if (candidate.First.Parent.DescendantTokens().Any(token =>
                candidate.Span.Contains(token.Span) && UserExpressionLayout.HasLineBreak(token.Text)))
        {
            return null;
        }
        foreach (var expression in candidate.First.Parent.DescendantNodes().OfType<ExpressionSyntax>()
                     .Where(expression => candidate.Span.Contains(expression.Span) &&
                         expression is InvocationExpressionSyntax or ObjectCreationExpressionSyntax))
        {
            var arguments = semanticModel.GetOperation(expression, cancellationToken) switch
            {
                IInvocationOperation invocation => invocation.Arguments,
                IObjectCreationOperation creation => creation.Arguments,
                _ => ImmutableArray<IArgumentOperation>.Empty
            };
            if (arguments.Any(argument => argument.IsImplicit && argument.Parameter is { } parameter &&
                    parameter.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() is
                        "System.Runtime.CompilerServices.CallerMemberNameAttribute" or
                        "System.Runtime.CompilerServices.CallerFilePathAttribute" or
                        "System.Runtime.CompilerServices.CallerLineNumberAttribute")))
            {
                return null;
            }
        }
        var parameters = ImmutableArray.CreateBuilder<Parameter>();
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        bool Add(ISymbol symbol, IdentifierNameSyntax? use = null)
        {
            if (!seen.Add(symbol)) return true;
            if (symbol.IsImplicitlyDeclared) return true;
            var type = symbol switch
            {
                IParameterSymbol { RefKind: RefKind.None } parameter => parameter.Type,
                ILocalSymbol { RefKind: RefKind.None, IsConst: false } local => local.Type,
                _ => null
            };
            var byReference = flow.CapturedOutside.Contains(symbol, SymbolEqualityComparer.Default);
            if (type is null || type is INamedTypeSymbol { IsAnonymousType: true } ||
                byReference && flow.CapturedInside.Contains(symbol, SymbolEqualityComparer.Default))
            {
                return false;
            }

            if (!byReference && use is not null && semanticModel.GetTypeInfo(use, cancellationToken).Nullability.FlowState ==
                NullableFlowState.NotNull)
            {
                type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
            }

            parameters.Add(new Parameter(symbol, TypeMapperMappingTypePolicy.GetGeneratedTypeName(type),
                SyntaxFacts.GetKeywordKind(symbol.Name) == SyntaxKind.None ? symbol.Name : "@" + symbol.Name,
                byReference));
            return true;
        }

        var methodSymbol = (IMethodSymbol)semanticModel.GetDeclaredSymbol(candidate.Method, cancellationToken)!;
        if (!Add(methodSymbol.Parameters.First(parameter => parameter.Name == mapping.NonNullSourceName))) return null;
        foreach (var identifier in identifiers)
        {
            var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
            if (symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction } localFunction &&
                localFunction.DeclaringSyntaxReferences.Any(reference => !candidate.Span.Contains(reference.Span)))
            {
                return null;
            }

            if (symbol is not (IParameterSymbol or ILocalSymbol) ||
                flow.VariablesDeclared.Contains(symbol, SymbolEqualityComparer.Default) ||
                symbol.DeclaringSyntaxReferences.Any(reference => candidate.Span.Contains(reference.Span)))
            {
                continue;
            }

            if (!Add(symbol, identifier)) return null;
        }

        if (!Add(methodSymbol.Parameters.First(parameter => parameter.Name == "context"))) return null;
        return new BoundCandidate(candidate, parameters.ToImmutable());
    }

    private static int Column(SourceText text, int position) =>
        position - text.Lines.GetLineFromPosition(position).Start;

    private static string Unindent(string value, int indent)
    {
        var lines = value.Replace("\r\n", "\n").Split('\n');
        for (var index = 1; index < lines.Length; index++)
        {
            var removed = 0;
            while (removed < indent && removed < lines[index].Length && lines[index][removed] == ' ') removed++;
            lines[index] = lines[index].Substring(removed);
        }

        return string.Join("\r\n", lines);
    }

    private sealed record Candidate(
        MethodDeclarationSyntax Method,
        StatementSyntax First,
        StatementSyntax Last,
        TextSpan Span,
        string Body);

    private sealed record Parameter(ISymbol Symbol, string TypeName, string Name, bool ByReference = false);

    private sealed record BoundCandidate(Candidate Syntax, ImmutableArray<Parameter> Parameters)
    {
        public string Key => Syntax.Body + "\n" + string.Join("\n",
            Parameters.Select(parameter => (parameter.ByReference ? "ref " : string.Empty) +
                parameter.TypeName + " " + parameter.Name));
    }
}
