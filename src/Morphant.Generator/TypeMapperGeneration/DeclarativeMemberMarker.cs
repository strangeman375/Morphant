using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DeclarativeMemberMarker
{
    public static bool TryGetKind(
        ExpressionSyntax expression,
        ITypeSymbol targetType,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken,
        out DeclarativeMemberMarkerKind kind)
    {
        if (DeclarativeIntrinsic.TryGetWrapperCast(
                expression,
                MetadataNames.Member,
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

        if (intrinsicKind == DeclarativeIntrinsicKind.Auto)
        {
            kind = DeclarativeMemberMarkerKind.Auto;
        }
        else if (intrinsicKind == DeclarativeIntrinsicKind.Ignore)
        {
            kind = DeclarativeMemberMarkerKind.Ignore;
        }
        else
        {
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

internal enum DeclarativeMemberMarkerKind
{
    Auto,
    Ignore
}
