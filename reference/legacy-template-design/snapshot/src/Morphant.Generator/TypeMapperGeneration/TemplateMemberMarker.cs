using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TemplateMemberMarker
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
        out TemplateMemberMarkerKind kind)
    {
        expression = UnwrapParentheses(expression);

        if (expression is not InvocationExpressionSyntax invocation ||
            semanticModel.GetSymbolInfo(
                    invocation,
                    cancellationToken)
                .Symbol is not IMethodSymbol
                {
                    ContainingType: { } containingType,
                    ReturnType: INamedTypeSymbol type
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
                type.OriginalDefinition);

        if (metadataName is
            AutoMarkerMetadataName or
            GenericAutoMarkerMetadataName)
        {
            kind = TemplateMemberMarkerKind.Auto;
            return true;
        }

        if (metadataName is
            IgnoreMarkerMetadataName or
            GenericIgnoreMarkerMetadataName)
        {
            kind = TemplateMemberMarkerKind.Ignore;
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

internal enum TemplateMemberMarkerKind
{
    Auto,
    Ignore
}
