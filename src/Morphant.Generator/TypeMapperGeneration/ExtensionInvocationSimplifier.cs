using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Morphant.Generator.TypeMapperGeneration;

// Plans keep the unambiguous static form. Mark only syntax synthesized from a
// user's extension call; explicit static calls are never readability candidates.
internal static class ExtensionInvocationSimplifier
{
    private const string CallMarker = "/*Morphant.ExtensionCall*/";
    private const string ConditionalMarker = "/*Morphant.ExtensionConditional*/";
    private const string BodyMarker = "/*Morphant.ExtensionBody*/";
    private const string NodeIdentity = "Morphant.ExtensionBinding";
    private const string ImportIdentity = "Morphant.ExtensionImport";

    public static SimpleNameSyntax MarkCall(SimpleNameSyntax name) =>
        name.WithIdentifier(Mark(name.Identifier, CallMarker));

    public static SyntaxToken MarkConditional(SyntaxToken token) => Mark(token, ConditionalMarker)
        .WithTrailingTrivia(SyntaxFactory.Comment(ConditionalMarker), SyntaxFactory.Space);

    public static BlockSyntax MarkBody(BlockSyntax body, SyntaxNode function,
        SyntaxToken arrow, ExpressionSyntax expression)
    {
        var layout = string.Join("\0", Indentation(function),
            arrow.GetPreviousToken().TrailingTrivia.ToFullString() + arrow.LeadingTrivia,
            arrow.TrailingTrivia.ToFullString() + expression.GetLeadingTrivia(),
            expression.GetTrailingTrivia().ToFullString(), function.GetTrailingTrivia().ToFullString());
        var marker = BodyMarker.Substring(0, BodyMarker.Length - 2) + ":" +
            Convert.ToBase64String(Encoding.UTF8.GetBytes(layout)) + "*/";
        return body.WithOpenBraceToken(Mark(body.OpenBraceToken, marker));
    }

    private static SyntaxToken Mark(SyntaxToken token, string marker) =>
        token.WithTrailingTrivia(token.TrailingTrivia.Add(SyntaxFactory.Comment(marker)));

    private static bool HasMarker(SyntaxToken token, string marker) =>
        token.HasAnnotations(marker);

    private static string Indentation(SyntaxNode node)
    {
        var text = node.SyntaxTree.GetText();
        var line = text.Lines.GetLineFromPosition(node.SpanStart);
        return new string(text.ToString(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(line.Start, node.SpanStart))
            .TakeWhile(character => character is ' ' or '\t').ToArray());
    }

    public static string Simplify(string source, CSharpCompilation compilation,
        CSharpParseOptions? options, CancellationToken cancellationToken)
    {
        if (source.IndexOf(CallMarker, StringComparison.Ordinal) < 0) return source;
        var parsed = ReadMarkers(CSharpSyntaxTree.ParseText(source, options, cancellationToken: cancellationToken)
            .GetCompilationUnitRoot(cancellationToken));
        var ordinal = 0;
        var root = parsed.ReplaceNodes(parsed.DescendantNodes().Where(Observe),
            (_, rewritten) => rewritten.WithAdditionalAnnotations(new SyntaxAnnotation(
                NodeIdentity, (ordinal++).ToString(CultureInfo.InvariantCulture))));
        var tree = CSharpSyntaxTree.Create(root, options);
        root = tree.GetCompilationUnitRoot(cancellationToken);
        var semantic = compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);
        var candidates = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(node => node.Expression is MemberAccessExpressionSyntax access && HasMarker(access.Name.Identifier, CallMarker))
            .Select(node => (Syntax: node, Method: (semantic.GetOperation(node, cancellationToken) as IInvocationOperation)?.TargetMethod))
            .Where(item => item.Method is { IsExtensionMethod: true, Parameters.Length: > 0 } &&
                item.Syntax.ArgumentList.Arguments.Count > 0 &&
                item.Syntax.ArgumentList.Arguments[0].NameColon is null)
            .ToDictionary(item => Identity(item.Syntax)!, item => item.Method!.ContainingType, StringComparer.Ordinal);
        if (candidates.Count == 0) return Clean(root);

        var bindings = root.GetAnnotatedNodes(NodeIdentity).ToDictionary(node => Identity(node)!,
            node => Binding(node, semantic, cancellationToken), StringComparer.Ordinal);
        var diagnostics = Diagnostics(semantic, cancellationToken);
        var enabled = new HashSet<string>(candidates.Keys, StringComparer.Ordinal);
        while (enabled.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rewritten = (CompilationUnitSyntax)new Rewriter(enabled).Visit(root)!;
            var candidateTree = CSharpSyntaxTree.Create(rewritten, options);
            rewritten = candidateTree.GetCompilationUnitRoot(cancellationToken);
            var candidateModel = compilation.AddSyntaxTrees(candidateTree).GetSemanticModel(candidateTree);
            var imports = rewritten.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(node => Identity(node) is { } id && enabled.Contains(id) &&
                    Binding(node, candidateModel, cancellationToken) != bindings[id])
                .Select(node => candidates[Identity(node)!])
                .GroupBy(TypeName, StringComparer.Ordinal).Select(group => group.First())
                .OrderBy(TypeName, StringComparer.Ordinal).ToArray();
            if (imports.Length != 0)
            {
                candidateTree = CSharpSyntaxTree.Create(AddImports(rewritten, imports), options);
                rewritten = candidateTree.GetCompilationUnitRoot(cancellationToken);
                candidateModel = compilation.AddSyntaxTrees(candidateTree).GetSemanticModel(candidateTree);
            }
            var rejected = new HashSet<string>(StringComparer.Ordinal);
            var rejectedImports = new HashSet<string>(StringComparer.Ordinal);

            void Reject(SyntaxNode node)
            {
                var call = node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>()
                    .Select(Identity).FirstOrDefault(id => id is not null && enabled.Contains(id));
                if (call is not null)
                {
                    rejected.Add(call);
                    return;
                }

                var import = node.AncestorsAndSelf().OfType<UsingDirectiveSyntax>()
                    .SelectMany(usingDirective => usingDirective.GetAnnotations(ImportIdentity)).FirstOrDefault()?.Data;
                if (import is not null)
                {
                    rejectedImports.Add(import);
                    return;
                }

                // An import can also rebind an existing query or method group.
                // Reject all contributing imports together, never pick a winner
                // based on registration order.
                var contributing = Symbols(node, candidateModel, cancellationToken)
                    .Select(symbol => symbol is INamedTypeSymbol type ? type.ContainingType : symbol.ContainingType)
                    .Where(type => type is not null).Select(type => TypeName(type!))
                    .Where(name => imports.Any(type => TypeName(type) == name)).ToArray();
                if (contributing.Length == 0) rejectedImports.UnionWith(imports.Select(TypeName));
                else rejectedImports.UnionWith(contributing);
            }

            foreach (var node in rewritten.GetAnnotatedNodes(NodeIdentity))
                if (bindings.TryGetValue(Identity(node)!, out var expected) &&
                    Binding(node, candidateModel, cancellationToken) != expected)
                    Reject(node);

            foreach (var group in candidateModel.GetDiagnostics(cancellationToken: cancellationToken)
                         .Where(IsDiagnostic).GroupBy(DiagnosticKey))
            {
                if (diagnostics.TryGetValue(group.Key, out var count) && group.Count() <= count) continue;
                foreach (var diagnostic in group)
                    Reject(rewritten.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true));
            }

            if (rejected.Count == 0 && rejectedImports.Count == 0) return Clean(rewritten);
            rejected.UnionWith(enabled.Where(id => rejectedImports.Contains(TypeName(candidates[id]))));
            if (rejected.Count == 0) break;
            enabled.ExceptWith(rejected);
        }

        return Clean(root);
    }

    private static CompilationUnitSyntax AddImports(CompilationUnitSyntax root, IEnumerable<INamedTypeSymbol> types)
    {
        var imports = new List<UsingDirectiveSyntax>();
        foreach (var type in types)
        {
            var name = TypeName(type);
            var warnings = ObsoleteTypeWarnings.Collect(type).Select(warning => warning.DiagnosticId)
                .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var text = (warnings.Length == 0 ? string.Empty : "#pragma warning disable " + string.Join(", ", warnings) + "\r\n") +
                "using static " + name + ";\r\n" +
                (warnings.Length == 0 ? string.Empty : "#pragma warning restore " + string.Join(", ", warnings) + "\r\n");
            var unit = SyntaxFactory.ParseCompilationUnit(text);
            imports.Add(unit.Usings[0].WithTrailingTrivia(unit.Usings[0].GetTrailingTrivia()
                    .AddRange(unit.EndOfFileToken.LeadingTrivia))
                .WithAdditionalAnnotations(new SyntaxAnnotation(ImportIdentity, name)));
        }

        var hasUsings = root.Usings.Count != 0;
        if (!hasUsings)
        {
            var first = root.GetFirstToken();
            imports[0] = imports[0].WithLeadingTrivia(first.LeadingTrivia.AddRange(imports[0].GetLeadingTrivia()));
            root = root.ReplaceToken(first, first.WithLeadingTrivia(default(SyntaxTriviaList)));
        }
        else
        {
            var last = root.Usings.Last();
            root = root.ReplaceNode(last, last.WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed));
        }
        if (!hasUsings)
            imports[imports.Count - 1] = imports[imports.Count - 1].WithTrailingTrivia(
                imports[imports.Count - 1].GetTrailingTrivia().Add(SyntaxFactory.CarriageReturnLineFeed));
        return root.WithUsings(root.Usings.AddRange(imports));
    }

    private static bool Observe(SyntaxNode node) => node is ExpressionSyntax or QueryClauseSyntax or SelectOrGroupClauseSyntax;
    private static string? Identity(SyntaxNode node) => node.GetAnnotations(NodeIdentity).FirstOrDefault()?.Data;
    private static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable);

    private static IEnumerable<ISymbol> Symbols(SyntaxNode node, SemanticModel semantic, CancellationToken token)
    {
        var info = semantic.GetSymbolInfo(node, token);
        if (info.Symbol is { } symbol) yield return symbol;
        foreach (var candidate in info.CandidateSymbols) yield return candidate;
        if (node is QueryClauseSyntax clause)
        {
            var query = semantic.GetQueryClauseInfo(clause, token);
            if (query.OperationInfo.Symbol is { } operation) yield return operation;
            foreach (var candidate in query.OperationInfo.CandidateSymbols) yield return candidate;
            if (query.CastInfo.Symbol is { } cast) yield return cast;
        }
    }

    private static string Binding(SyntaxNode node, SemanticModel semantic, CancellationToken token)
    {
        var symbols = string.Join(";", Symbols(node, semantic, token).Select(Symbol));
        var type = node is ExpressionSyntax expression ? semantic.GetTypeInfo(expression, token) : default;
        // The null lift belongs to the complete conditional expression. Its
        // non-null arm converts T to T? in the lowered form, whereas ?. lifts
        // outside WhenNotNull. Observe that conversion at the enclosing node.
        var converted = node.Parent is ConditionalExpressionSyntax conditional && conditional.WhenTrue == node &&
                Rewriter.IsConditional(conditional.Condition) ||
            node.Parent is ConditionalAccessExpressionSyntax access && access.WhenNotNull == node ||
            node.Parent is MemberAccessExpressionSyntax member && member.Name == node ||
            node.Parent is MemberBindingExpressionSyntax binding && binding.Name == node
                ? type.Type : type.ConvertedType;
        var result = symbols + "|" + (type.Type is null ? "" : TypeName(type.Type)) + "|" +
            (converted is null ? "" : TypeName(converted));
        if (node is InvocationExpressionSyntax && semantic.GetOperation(node, token) is IInvocationOperation invocation)
            result += "|" + Invocation(invocation);
        return result;
    }

    private static string Symbol(ISymbol symbol)
    {
        if (symbol is IMethodSymbol { ReducedFrom: { } reduced } method)
            symbol = method.IsGenericMethod ? reduced.ConstructedFrom.Construct(method.TypeArguments, method.TypeArgumentNullableAnnotations) : reduced;
        return symbol.Kind + ":" + symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat
                .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier)) +
            ":" + symbol.ContainingAssembly?.Identity;
    }

    private static string Invocation(IInvocationOperation operation) =>
        Symbol(operation.TargetMethod) + "|" + string.Join(";", operation.Arguments.Select(argument =>
            argument.Parameter?.Name + ":" + argument.Parameter?.RefKind + ":" + argument.ArgumentKind + ":" +
            (argument.Value.Type is null ? "" : TypeName(argument.Value.Type)) + ":" +
            Conversion(argument.InConversion) + ":" + Conversion(argument.OutConversion) + ":" + ValueConversions(argument.Value)));

    private static string ValueConversions(IOperation value) => value is IConversionOperation conversion
        ? Conversion(conversion.Conversion) + ":" + conversion.IsChecked + ":" + conversion.IsTryCast + ":" +
            (conversion.Operand.Type is null ? "" : TypeName(conversion.Operand.Type)) + ":" + ValueConversions(conversion.Operand)
        : string.Empty;

    private static string Conversion(CommonConversion conversion) =>
        conversion.Exists + ":" + conversion.IsIdentity + ":" + conversion.IsNumeric + ":" +
        conversion.IsReference + ":" + (conversion.MethodSymbol is { } method ? Symbol(method) : "");

    private static bool IsDiagnostic(Diagnostic diagnostic) => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error;
    private static string DiagnosticKey(Diagnostic diagnostic) => diagnostic.Id + ":" + diagnostic.GetMessage(CultureInfo.InvariantCulture);
    private static Dictionary<string, int> Diagnostics(SemanticModel semantic, CancellationToken token) =>
        semantic.GetDiagnostics(cancellationToken: token).Where(IsDiagnostic).GroupBy(DiagnosticKey)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static string Clean(SyntaxNode root) => root.ToFullString();

    private static CompilationUnitSyntax ReadMarkers(CompilationUnitSyntax root)
    {
        var tokens = root.DescendantTokens().ToArray();
        var replacements = new Dictionary<SyntaxToken, SyntaxToken>();
        for (var index = 1; index < tokens.Length; index++)
        {
            var previous = tokens[index - 1];
            var current = tokens[index];
            var gap = previous.TrailingTrivia.AddRange(current.LeadingTrivia);
            var bodyPrefix = BodyMarker.Substring(0, BodyMarker.Length - 2) + ":";
            var marker = gap.FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) &&
                (trivia.ToString() is CallMarker or ConditionalMarker || trivia.ToString().StartsWith(bodyPrefix, StringComparison.Ordinal)));
            if (marker == default) continue;

            var text = marker.ToString();
            var annotation = text.StartsWith(bodyPrefix, StringComparison.Ordinal)
                ? new SyntaxAnnotation(BodyMarker, Encoding.UTF8.GetString(Convert.FromBase64String(
                    text.Substring(bodyPrefix.Length, text.Length - bodyPrefix.Length - 2))))
                : new SyntaxAnnotation(text);
            gap = gap.Remove(marker);
            if (gap.All(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia)))
                gap = text == ConditionalMarker ? SyntaxFactory.TriviaList(SyntaxFactory.Space) : default;
            replacements[previous] = (replacements.TryGetValue(previous, out var before) ? before : previous)
                .WithTrailingTrivia(default(SyntaxTriviaList)).WithAdditionalAnnotations(annotation);
            replacements[current] = current.WithLeadingTrivia(gap);
        }
        return root.ReplaceTokens(replacements.Keys, (token, _) => replacements[token]);
    }

    private sealed class Rewriter : CSharpSyntaxRewriter
    {
        private readonly ISet<string> _enabled;
        public Rewriter(ISet<string> enabled) => _enabled = enabled;

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (Identity(node) is not { } id || !_enabled.Contains(id) ||
                rewritten.Expression is not MemberAccessExpressionSyntax access) return rewritten;
            var arguments = rewritten.ArgumentList.Arguments;
            var receiver = arguments[0].Expression.WithTrailingTrivia(access.Expression.GetTrailingTrivia());
            return rewritten.WithExpression(access.WithExpression(receiver))
                .WithArgumentList(rewritten.ArgumentList.WithArguments(arguments.RemoveAt(0)))
                .WithTriviaFrom(node);
        }

        public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
        {
            if (node.Expression is ConditionalExpressionSyntax conditional && IsConditional(conditional.Condition))
            {
                var rewritten = (ExpressionSyntax)Visit(conditional)!;
                if (rewritten is ConditionalAccessExpressionSyntax) return rewritten.WithTriviaFrom(node);
                return node.WithExpression(rewritten);
            }
            return base.VisitParenthesizedExpression(node);
        }

        public override SyntaxNode? VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            var rewritten = (ConditionalExpressionSyntax)base.VisitConditionalExpression(node)!;
            return TryRestore(rewritten.Condition, rewritten.WhenTrue) is { } restored
                ? node.CopyAnnotationsTo(restored.WithTriviaFrom(node)) : rewritten;
        }

        public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
        {
            var rewritten = (IfStatementSyntax)base.VisitIfStatement(node)!;
            return rewritten.Else is null && rewritten.Statement is BlockSyntax { Statements.Count: 1 } block &&
                block.Statements[0] is ExpressionStatementSyntax expression &&
                TryRestore(rewritten.Condition, expression.Expression) is { } restored
                ? SyntaxFactory.ExpressionStatement(restored).WithTriviaFrom(node) : rewritten;
        }

        public static bool IsConditional(ExpressionSyntax condition) => condition is IsPatternExpressionSyntax
            { Pattern: RecursivePatternSyntax { Designation: SingleVariableDesignationSyntax designation } } &&
            HasMarker(designation.Identifier, ConditionalMarker);

        private static ConditionalAccessExpressionSyntax? TryRestore(ExpressionSyntax condition, ExpressionSyntax continuation)
        {
            if (!IsConditional(condition)) return null;
            var pattern = (IsPatternExpressionSyntax)condition;
            var designation = (SingleVariableDesignationSyntax)((RecursivePatternSyntax)pattern.Pattern).Designation!;
            var receivers = continuation.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                .Where(identifier => identifier.Identifier.ValueText == designation.Identifier.ValueText).ToArray();
            if (receivers.Length != 1) return null;
            var receiver = receivers[0];
            var binding = receiver.Parent switch
            {
                MemberAccessExpressionSyntax access when access.Expression == receiver =>
                    (ExpressionSyntax)access.CopyAnnotationsTo(SyntaxFactory.MemberBindingExpression(access.OperatorToken, access.Name)),
                ElementAccessExpressionSyntax element when element.Expression == receiver =>
                    element.CopyAnnotationsTo(SyntaxFactory.ElementBindingExpression(element.ArgumentList)),
                _ => null
            };
            if (binding is null) return null;
            var body = continuation.ReplaceNode(receiver.Parent!, binding);
            return SyntaxFactory.ConditionalAccessExpression(pattern.Expression.WithoutTrailingTrivia(), body);
        }

        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            var rewritten = (SimpleLambdaExpressionSyntax)base.VisitSimpleLambdaExpression(node)!;
            return ExpressionBody(rewritten.Body, node) is { } body
                ? rewritten.WithParameter(rewritten.Parameter.WithoutTrailingTrivia())
                    .WithArrowToken(body.Arrow).WithBody(body.Expression) : rewritten;
        }

        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            var rewritten = (ParenthesizedLambdaExpressionSyntax)base.VisitParenthesizedLambdaExpression(node)!;
            return ExpressionBody(rewritten.Body, node) is { } body
                ? rewritten.WithParameterList(rewritten.ParameterList.WithoutTrailingTrivia())
                    .WithArrowToken(body.Arrow).WithBody(body.Expression) : rewritten;
        }

        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            var rewritten = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;
            return rewritten.Body is { } block && ExpressionBody(block, node) is { } body
                ? rewritten.WithBody(null).WithParameterList(rewritten.ParameterList.WithoutTrailingTrivia())
                    .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(body.Expression).WithArrowToken(body.Arrow))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(body.Trailing))
                : rewritten;
        }

        private static (ExpressionSyntax Expression, SyntaxToken Arrow, SyntaxTriviaList Trailing)? ExpressionBody(
            CSharpSyntaxNode body, SyntaxNode function)
        {
            if (body is not BlockSyntax { Statements.Count: 1 } block ||
                block.OpenBraceToken.GetAnnotations(BodyMarker).FirstOrDefault()?.Data is not { } data ||
                block.Statements[0] is not ExpressionStatementSyntax { Expression: ConditionalAccessExpressionSyntax expression })
                return null;

            var layout = data.Split('\0');
            var indentation = Indentation(function);
            SyntaxTriviaList Trivia(int index) => SyntaxFactory.ParseLeadingTrivia(layout[index]
                .Replace("\r\n", "\n").Replace("\r", "\n")
                .Replace("\n" + layout[0], "\n" + indentation).Replace("\n", "\r\n"));
            return (expression.WithoutTrivia().WithTrailingTrivia(Trivia(3)),
                SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken).WithLeadingTrivia(Trivia(1)).WithTrailingTrivia(Trivia(2)),
                Trivia(4));
        }
    }
}
