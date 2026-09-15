using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class PrivateHelperParameters
{
    public static string RemoveUnused(string source, CSharpCompilation compilation,
        CSharpParseOptions? options, CancellationToken cancellationToken)
    {
        var tree = CSharpSyntaxTree.ParseText(source, options, cancellationToken: cancellationToken);
        var root = tree.GetRoot(cancellationToken);
        var semantic = compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);
        var methods = new Dictionary<IMethodSymbol, MethodDeclarationSyntax>(SymbolEqualityComparer.Default);
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (method.Modifiers.Any(SyntaxKind.PrivateKeyword) && method.AttributeLists.Count == 0 &&
                method.ExplicitInterfaceSpecifier is null &&
                semantic.GetDeclaredSymbol(method, cancellationToken) is { } symbol)
                methods.Add(symbol, method);
        }
        if (methods.Count == 0) return source;

        var parameters = new HashSet<IParameterSymbol>(methods.Keys.SelectMany(method => method.Parameters),
            SymbolEqualityComparer.Default);
        var live = new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default);
        var dependencies = new List<(IParameterSymbol Callee, IParameterSymbol Caller)>();
        var calls = new List<(InvocationExpressionSyntax Syntax, IInvocationOperation Operation)>();
        var names = new HashSet<string>(methods.Keys.Select(method => method.Name), StringComparer.Ordinal);

        foreach (var method in methods.Keys)
        {
            foreach (var parameter in method.Parameters)
                if (parameter.RefKind != RefKind.None || parameter.IsParams || parameter.GetAttributes().Length != 0)
                    live.Add(parameter);
        }

        foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>().Where(name => names.Contains(name.Identifier.ValueText)))
        {
            if (semantic.GetSymbolInfo(name, cancellationToken).Symbol is not IMethodSymbol called ||
                !methods.ContainsKey(called.OriginalDefinition)) continue;
            var expression = name.Parent is MemberAccessExpressionSyntax access && access.Name == name
                ? (ExpressionSyntax)access : name;
            if (expression.Parent is not InvocationExpressionSyntax invocation || invocation.Expression != expression ||
                semantic.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation)
            {
                // Method groups retain their delegate-compatible signature.
                live.UnionWith(called.OriginalDefinition.Parameters);
                continue;
            }
            calls.Add((invocation, operation));
            foreach (var argument in operation.Arguments)
            {
                if (argument.Parameter is not { } parameter || argument.IsImplicit) continue;
                var declarationParameter = called.OriginalDefinition.Parameters[parameter.Ordinal];
                if (!CanDiscard(argument)) live.Add(declarationParameter);
            }
        }

        foreach (var method in methods.Values)
        {
            foreach (var identifier in method.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (semantic.GetSymbolInfo(identifier, cancellationToken).Symbol is not IParameterSymbol parameter ||
                    !parameters.Contains(parameter)) continue;
                if (identifier.Parent is ArgumentSyntax argumentSyntax && argumentSyntax.Expression == identifier &&
                    semantic.GetOperation(argumentSyntax, cancellationToken) is IArgumentOperation argument &&
                    argument.Parent is IInvocationOperation call &&
                    methods.ContainsKey(call.TargetMethod.OriginalDefinition) &&
                    argument.Parameter is { } target && CanDiscard(argument))
                    dependencies.Add((call.TargetMethod.OriginalDefinition.Parameters[target.Ordinal], parameter));
                else live.Add(parameter);
            }
        }

        bool changed;
        do
        {
            changed = false;
            foreach (var dependency in dependencies)
                if (live.Contains(dependency.Callee)) changed |= live.Add(dependency.Caller);
        } while (changed);

        var changes = new List<TextChange>();
        foreach (var pair in methods)
            RemoveItems(pair.Value.ParameterList.Parameters,
                index => !live.Contains(pair.Key.Parameters[index]),
                pair.Value.ParameterList.OpenParenToken, pair.Value.ParameterList.CloseParenToken, changes);
        foreach (var call in calls)
        {
            var removable = new HashSet<int>();
            foreach (var argument in call.Operation.Arguments)
            {
                if (argument.IsImplicit || argument.Parameter is not { } parameter ||
                    live.Contains(call.Operation.TargetMethod.OriginalDefinition.Parameters[parameter.Ordinal])) continue;
                var index = call.Syntax.ArgumentList.Arguments.IndexOf((ArgumentSyntax)argument.Syntax);
                if (index >= 0) removable.Add(index);
            }
            RemoveItems(call.Syntax.ArgumentList.Arguments, removable.Contains,
                call.Syntax.ArgumentList.OpenParenToken, call.Syntax.ArgumentList.CloseParenToken, changes);
        }
        return changes.Count == 0 ? source : tree.GetText(cancellationToken).WithChanges(changes).ToString();
    }

    private static bool CanDiscard(IArgumentOperation argument)
    {
        if (!argument.InConversion.IsIdentity || argument.Parameter?.RefKind != RefKind.None ||
            argument.Syntax is not ArgumentSyntax syntax || syntax.RefKindKeyword.RawKind != 0 ||
            syntax.DescendantTrivia().Any(trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia) &&
                !trivia.IsKind(SyntaxKind.EndOfLineTrivia))) return false;
        return argument.Value is ILocalReferenceOperation or IParameterReferenceOperation or ILiteralOperation or IDefaultValueOperation;
    }

    private static void RemoveItems<T>(SeparatedSyntaxList<T> items, Func<int, bool> remove,
        SyntaxToken open, SyntaxToken close, List<TextChange> changes) where T : SyntaxNode
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (!remove(index)) continue;
            var first = index;
            while (index + 1 < items.Count && remove(index + 1)) index++;
            var span = first == 0 && index == items.Count - 1
                ? TextSpan.FromBounds(open.Span.End, close.SpanStart)
                : index == items.Count - 1
                    ? TextSpan.FromBounds(items.GetSeparator(first - 1).SpanStart, items[index].Span.End)
                    : TextSpan.FromBounds(items[first].SpanStart, items[index + 1].SpanStart);
            changes.Add(new TextChange(span, string.Empty));
        }
    }
}
