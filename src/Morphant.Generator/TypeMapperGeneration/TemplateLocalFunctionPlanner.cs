using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TemplateLocalFunctionPlanner
{
    private const string UnsupportedBodyMessage =
        "Static local functions currently support only expression " +
        "bodies or local variable declarations followed by a " +
        "single return statement.";

    public static TemplateLocalFunctionBuildResult Build(
        LambdaExpressionSyntax templateLambda,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var functions =
            new List<(
                IMethodSymbol Symbol,
                LocalFunctionStatementSyntax Syntax,
                string? UnsupportedMessage
            )>();
        var seen =
            new HashSet<ISymbol>(
                SymbolEqualityComparer.Default);
        var pending = new Queue<IMethodSymbol>();

        AddReferencedFunctions(
            templateLambda,
            semanticModel,
            seen,
            pending,
            cancellationToken);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var function = pending.Dequeue();

            if (!function.IsStatic ||
                function.DeclaringSyntaxReferences
                    .FirstOrDefault()?
                    .GetSyntax(cancellationToken) is not
                    LocalFunctionStatementSyntax syntax)
            {
                return new UnsupportedTemplateLocalFunctions(
                    UnsupportedBodyMessage);
            }

            functions.Add((
                function,
                syntax,
                TransferableLambdaSyntax.HasSupportedBody(syntax)
                    ? null
                    : UnsupportedBodyMessage));
            AddReferencedFunctions(
                syntax,
                semanticModel,
                seen,
                pending,
                cancellationToken);
        }

        var reservedNames =
            new HashSet<string>(
                templateLambda.DescendantTokens()
                    .Where(static token =>
                        token.IsKind(
                            SyntaxKind.IdentifierToken))
                    .Select(static token =>
                        token.ValueText),
                StringComparer.Ordinal);

        foreach (var function in functions)
        {
            foreach (var identifier in function.Syntax
                         .DescendantTokens()
                         .Where(static token =>
                             token.IsKind(
                                 SyntaxKind.IdentifierToken)))
            {
                reservedNames.Add(identifier.ValueText);
            }
        }

        var placeholders =
            new Dictionary<ISymbol, string>(
                SymbolEqualityComparer.Default);
        var result =
            ImmutableArray.CreateBuilder<
                TemplateLocalFunctionSyntaxPlan>(
                functions.Count);
        var ordinal = 0;

        foreach (var function in functions)
        {
            var placeholder = AllocatePlaceholder(
                ref ordinal,
                reservedNames);

            placeholders.Add(
                function.Symbol,
                placeholder);
            result.Add(
                new TemplateLocalFunctionSyntaxPlan(
                    placeholder,
                    function.Symbol.Name,
                    function.Syntax,
                    function.UnsupportedMessage));
        }

        return new SupportedTemplateLocalFunctions(
            result.ToImmutable(),
            placeholders);
    }

    private static void AddReferencedFunctions(
        SyntaxNode syntax,
        SemanticModel semanticModel,
        HashSet<ISymbol> seen,
        Queue<IMethodSymbol> pending,
        CancellationToken cancellationToken)
    {
        foreach (var name in syntax
                     .DescendantNodesAndSelf()
                     .OfType<SimpleNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbolInfo = semanticModel.GetSymbolInfo(
                name,
                cancellationToken);
            var candidates =
                symbolInfo.Symbol is { } symbol
                    ? symbolInfo.CandidateSymbols
                        .Insert(0, symbol)
                    : symbolInfo.CandidateSymbols;

            if (name.Parent is InvocationExpressionSyntax
                {
                    Expression: var invocationExpression
                } invocation &&
                ReferenceEquals(
                    invocationExpression,
                    name))
            {
                var invocationInfo =
                    semanticModel.GetSymbolInfo(
                        invocation,
                        cancellationToken);

                if (invocationInfo.Symbol is { } invocationSymbol)
                {
                    candidates = candidates.Add(
                        invocationSymbol);
                }

                candidates = candidates.AddRange(
                    invocationInfo.CandidateSymbols);
            }

            var function = candidates
                .OfType<IMethodSymbol>()
                .FirstOrDefault(method =>
                    method.IsStatic &&
                    method.DeclaringSyntaxReferences.Any(
                        reference =>
                            reference.GetSyntax(
                                cancellationToken) is
                                LocalFunctionStatementSyntax));

            if (function is null ||
                !seen.Add(function))
            {
                continue;
            }

            pending.Enqueue(function);
        }
    }

    private static string AllocatePlaceholder(
        ref int ordinal,
        HashSet<string> reservedNames)
    {
        while (true)
        {
            var candidate =
                "__morphantLocalFunction" +
                ordinal++.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);

            if (reservedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }
}

internal abstract record TemplateLocalFunctionBuildResult;

internal sealed record UnsupportedTemplateLocalFunctions(
    string Message)
    : TemplateLocalFunctionBuildResult;

internal sealed record SupportedTemplateLocalFunctions(
    ImmutableArray<TemplateLocalFunctionSyntaxPlan> Functions,
    IReadOnlyDictionary<ISymbol, string> Placeholders)
    : TemplateLocalFunctionBuildResult;

internal readonly record struct TemplateLocalFunctionSyntaxPlan(
    string PlaceholderName,
    string PreferredName,
    LocalFunctionStatementSyntax Syntax,
    string? UnsupportedMessage);
