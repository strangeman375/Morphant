using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TransferableLambdaSyntax
{
    public static bool TryGetCaptures(
        SyntaxNode transferredSyntax,
        SemanticModel semanticModel,
        HashSet<ISymbol> allowedCapturedSymbols,
        CancellationToken cancellationToken,
        out ImmutableArray<ISymbol> captures)
    {
        var result = ImmutableArray.CreateBuilder<ISymbol>();
        var seen = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);

        foreach (var name in transferredSyntax
                     .DescendantNodesAndSelf()
                     .OfType<SimpleNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = semanticModel.GetSymbolInfo(
                    name,
                    cancellationToken)
                .Symbol;

            if (symbol is null &&
                name.Parent is InvocationExpressionSyntax invocation &&
                ReferenceEquals(invocation.Expression, name))
            {
                symbol = semanticModel.GetSymbolInfo(
                        invocation,
                        cancellationToken)
                    .Symbol;
            }

            if (symbol is ILocalSymbol
                {
                    IsConst: true
                })
            {
                continue;
            }

            if (symbol is IMethodSymbol
                {
                    MethodKind: MethodKind.LocalFunction
                } or IRangeVariableSymbol)
            {
                if (!IsDeclaredWithin(symbol, transferredSyntax))
                {
                    captures = default;
                    return false;
                }

                continue;
            }

            if (symbol is not (ILocalSymbol or IParameterSymbol) ||
                IsDeclaredWithin(symbol, transferredSyntax))
            {
                continue;
            }

            if (!allowedCapturedSymbols.Contains(symbol))
            {
                captures = default;
                return false;
            }

            if (seen.Add(symbol))
            {
                result.Add(symbol);
            }
        }

        captures = result.ToImmutable();
        return true;
    }

    private static bool IsDeclaredWithin(
        ISymbol symbol,
        SyntaxNode syntax)
    {
        return symbol.DeclaringSyntaxReferences.Any(reference =>
            ReferenceEquals(reference.SyntaxTree, syntax.SyntaxTree) &&
            syntax.FullSpan.Contains(reference.Span));
    }
}
