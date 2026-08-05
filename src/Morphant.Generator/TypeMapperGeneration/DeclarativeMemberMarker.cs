using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DeclarativeMemberMarker
{
    private const string TypeMapperMetadataName =
        "Morphant.TypeMapper";

    private const string AutoMarkerMetadataName =
        "Morphant.Markers.AutoMarker";

    private const string GenericAutoMarkerMetadataName =
        "Morphant.Markers.AutoMarker`1";

    private const string IgnoreMarkerMetadataName =
        "Morphant.Markers.IgnoreMarker";

    private const string GenericIgnoreMarkerMetadataName =
        "Morphant.Markers.IgnoreMarker`1";

    public static bool TryGetKind(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out DeclarativeMemberMarkerKind kind)
    {
        expression = UnwrapParentheses(expression);

        if (expression is not InvocationExpressionSyntax invocation ||
            semanticModel.GetSymbolInfo(
                    invocation,
                    cancellationToken)
                .Symbol is not IMethodSymbol
                {
                    ContainingType: { } containingType,
                    ReturnType: INamedTypeSymbol returnType
                } ||
            !StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(containingType),
                TypeMapperMetadataName))
        {
            kind = default;
            return false;
        }

        var metadataName = SymbolNameHelper.GetFullMetadataName(
            returnType.OriginalDefinition);

        if (metadataName is
            AutoMarkerMetadataName or
            GenericAutoMarkerMetadataName)
        {
            kind = DeclarativeMemberMarkerKind.Auto;
            return true;
        }

        if (metadataName is
            IgnoreMarkerMetadataName or
            GenericIgnoreMarkerMetadataName)
        {
            kind = DeclarativeMemberMarkerKind.Ignore;
            return true;
        }

        kind = default;
        return false;
    }

    private static ExpressionSyntax UnwrapParentheses(
        ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}

internal enum DeclarativeMemberMarkerKind
{
    Auto,
    Ignore
}
