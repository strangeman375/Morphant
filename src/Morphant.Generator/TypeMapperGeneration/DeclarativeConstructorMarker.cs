using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DeclarativeConstructorMarker
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

    private const string MapMarkerMetadataName =
        "Morphant.Markers.MapMarker";

    private const string GenericMapMarkerMetadataName =
        "Morphant.Markers.MapMarker`1";

    public static bool TryGetKind(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out DeclarativeConstructorMarkerKind kind)
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

        var metadataName =
            SymbolNameHelper.GetFullMetadataName(
                returnType.OriginalDefinition);

        switch (metadataName)
        {
            case AutoMarkerMetadataName:
            case GenericAutoMarkerMetadataName:
                kind = DeclarativeConstructorMarkerKind.Auto;
                return true;

            case IgnoreMarkerMetadataName:
            case GenericIgnoreMarkerMetadataName:
                kind = DeclarativeConstructorMarkerKind.Ignore;
                return true;

            case MapMarkerMetadataName:
            case GenericMapMarkerMetadataName:
                kind = DeclarativeConstructorMarkerKind.Map;
                return true;

            default:
                kind = default;
                return false;
        }
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

internal enum DeclarativeConstructorMarkerKind
{
    Auto,
    Ignore,
    Map
}
