using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DeclarativeConstructorMarker
{
    public static bool TryGetKind(
        ExpressionSyntax expression,
        ITypeSymbol targetType,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken,
        out DeclarativeConstructorMarkerKind kind)
    {
        if (DeclarativeIntrinsic.TryGetWrapperCast(
                expression,
                MetadataNames.ConstructorParameter,
                semanticModel,
                cancellationToken,
                out var wrapperCast,
                out _))
        {
            expression = wrapperCast.Expression;
        }

        expression = DeclarativeIntrinsic.UnwrapTransparentSyntax(
            expression);

        if (expression is not InvocationExpressionSyntax invocation ||
            !DeclarativeIntrinsic.TryGetKind(
                invocation,
                semanticModel,
                cancellationToken,
                out var intrinsicKind,
                out _))
        {
            kind = default;
            return false;
        }

        switch (intrinsicKind)
        {
            case DeclarativeIntrinsicKind.Auto:
                kind = DeclarativeConstructorMarkerKind.Auto;
                break;

            case DeclarativeIntrinsicKind.Ignore:
                kind = DeclarativeConstructorMarkerKind.Ignore;
                break;

            case DeclarativeIntrinsicKind.Map:
            case DeclarativeIntrinsicKind.Create:
            case DeclarativeIntrinsicKind.Update:
                kind = DeclarativeConstructorMarkerKind.Map;
                return true;

            default:
                kind = default;
                return false;
        }

        if (semanticModel.GetOperation(
                invocation,
                cancellationToken) is IInvocationOperation
            {
                TargetMethod:
                {
                    IsGenericMethod: true,
                    TypeArguments.Length: 1
                } markerMethod
            })
        {
            var assertedType = markerMethod.TypeArguments[0]
                .WithNullableAnnotation(
                    markerMethod.TypeArgumentNullableAnnotations[0]);

            if (!DeclarativeIntrinsic.HasExactTargetType(
                    assertedType,
                    targetType,
                    semanticModel,
                    mapperType))
            {
                kind = default;
                return false;
            }
        }

        return true;
    }
}

internal enum DeclarativeConstructorMarkerKind
{
    Auto,
    Ignore,
    Map
}
