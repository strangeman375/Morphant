using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.MappingPair;

internal static class GeneratedMappingExtensionBinding
{
    public static bool IsAssignedMethod(
        IMethodSymbol? method,
        InvocationExpressionSyntax registration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return method is not null &&
               GeneratedMappingExtensionNaming.IsGeneratedMethod(
                   method, semanticModel.Compilation) &&
               method.ReturnType is INamedTypeSymbol builder &&
               semanticModel.GetSymbolInfo(registration, cancellationToken).Symbol is
                   IMethodSymbol { TypeArguments.Length: 2 } map &&
               HaveSamePresentation(builder.TypeArguments[0], map.TypeArguments[0]) &&
               HaveSamePresentation(builder.TypeArguments[1], map.TypeArguments[1]);
    }

    private static bool HaveSamePresentation(ITypeSymbol left, ITypeSymbol right)
    {
        return MappingTypeIdentityPolicy.Create(left) ==
                   MappingTypeIdentityPolicy.Create(right) &&
               StringComparer.Ordinal.Equals(
                   BclTupleShapePolicy.BuildPresentationKey(MappingTypeNormalization.WithoutOuterAnnotation(left)),
                   BclTupleShapePolicy.BuildPresentationKey(MappingTypeNormalization.WithoutOuterAnnotation(right)));
    }
}
