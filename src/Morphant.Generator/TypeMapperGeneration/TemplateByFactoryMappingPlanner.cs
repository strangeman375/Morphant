using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TemplateByFactoryMappingPlanner
{
    private const string ByFactoryMarkerMetadataName =
        "Morphant.Markers.IByFactoryMarker`1";

    private const string TypeMapperMetadataName =
        "Morphant.TypeMapper";

    public static bool TryBuild(
        ImmutableArray<TemplateObjectArgumentSyntax> arguments,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, string> rewriteExpression,
        CancellationToken cancellationToken,
        out string? factoryExpression)
    {
        factoryExpression = null;

        if (!ContainsMarker(
                arguments,
                semanticModel,
                cancellationToken))
        {
            return false;
        }

        if (arguments.Length != 1)
        {
            return true;
        }

        var markerArgument =
            arguments[0];

        if (markerArgument.Syntax.NameColon is
                { Name.Identifier.ValueText: not "marker" } ||
            !IsMarker(
                markerArgument.Value,
                semanticModel,
                cancellationToken) ||
            UnwrapParentheses(markerArgument.Value) is not
                InvocationExpressionSyntax markerInvocation ||
            !IsByFactoryInvocation(
                markerInvocation,
                semanticModel,
                cancellationToken) ||
            markerInvocation.ArgumentList.Arguments.Count != 1)
        {
            return true;
        }

        var factoryArgument =
            markerInvocation.ArgumentList.Arguments[0];

        if (factoryArgument.NameColon is
                { Name.Identifier.ValueText: not "factory" } ||
            UnwrapParentheses(factoryArgument.Expression) is not
                ParenthesizedLambdaExpressionSyntax
                {
                    ParameterList.Parameters.Count: 0,
                    ExpressionBody: { } expression
                })
        {
            return true;
        }

        factoryExpression = rewriteExpression(expression);
        return true;
    }

    private static bool ContainsMarker(
        ImmutableArray<TemplateObjectArgumentSyntax> arguments,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsMarker(
                    argument.Value,
                    semanticModel,
                    cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMarker(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        expression = UnwrapParentheses(expression);

        var typeInfo = semanticModel.GetTypeInfo(
            expression,
            cancellationToken);

        return IsMarkerType(typeInfo.Type) ||
               IsMarkerType(typeInfo.ConvertedType) ||
               semanticModel.GetSymbolInfo(
                       expression,
                       cancellationToken)
                   .Symbol is IMethodSymbol method &&
               IsMarkerType(method.ReturnType);
    }

    private static bool IsMarkerType(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol namedType &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       namedType.OriginalDefinition),
                   ByFactoryMarkerMetadataName);
    }

    private static bool IsByFactoryInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return semanticModel.GetSymbolInfo(
                    invocation,
                    cancellationToken)
                .Symbol is IMethodSymbol
                {
                    Name: "ByFactory",
                    ContainingType: { } containingType
                } &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       containingType),
                   TypeMapperMetadataName);
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
