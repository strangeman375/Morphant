using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.MappingPair;

internal static class GeneratedMappingExtensionBinding
{
    public static MappingSurfaceModel? FindAssignedSurface(
        MappingPairModel pair,
        ImmutableArray<CanonicalMappingPairCandidate> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (HaveSamePresentation(candidate.EffectiveSourceType, pair.SourceType) &&
                HaveSamePresentation(candidate.EffectiveDestinationType, pair.DestinationType))
            {
                return candidate.Surface;
            }
        }

        return null;
    }

    public static bool IsAssignedMethod(
        IMethodSymbol? method,
        InvocationExpressionSyntax registration,
        MappingSurfaceModel? assignedSurface,
        INamedTypeSymbol declaringMapperType,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (method is null || assignedSurface is not { } surface ||
            !GeneratedMappingExtensionNaming.IsGeneratedMethod(
                method, semanticModel.Compilation) ||
            method.ReturnType is not INamedTypeSymbol returnBuilder ||
            semanticModel.GetSymbolInfo(registration, cancellationToken).Symbol is not
                IMethodSymbol { TypeArguments.Length: 2 } mapMethod ||
            !HaveSamePresentation(returnBuilder.TypeArguments[1], mapMethod.TypeArguments[0]) ||
            !HaveSamePresentation(returnBuilder.TypeArguments[2], mapMethod.TypeArguments[1]))
        {
            return false;
        }

        var expectedOwnerName = SymbolNameHelper.GetFullMetadataName(
            surface.DeclaringMapperType.OriginalDefinition);
        INamedTypeSymbol? owner = declaringMapperType;

        while (owner is not null &&
               !StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(owner.OriginalDefinition),
                   expectedOwnerName))
        {
            owner = owner.BaseType;
        }

        if (owner is null)
        {
            return false;
        }

        var expectedSelf = MappingSurfacePolicy.FindMapperSelfType(owner);
        var receiver = method.ReducedFrom is not null
            ? method.ReceiverType as INamedTypeSymbol
            : method.Parameters[0].Type as INamedTypeSymbol;
        var expectedReceiverOwner = surface.Kind == MappingSurfaceKind.MapperFamilyScoped
            ? owner
            : expectedSelf;

        return receiver is not null &&
               SymbolEqualityComparer.Default.Equals(
                   returnBuilder.TypeArguments[0], expectedSelf) &&
               SymbolEqualityComparer.Default.Equals(
                   receiver.TypeArguments[0], expectedReceiverOwner);
    }

    private static bool HaveSamePresentation(ITypeSymbol left, ITypeSymbol right)
    {
        return MappingTypeIdentityPolicy.Create(left) ==
                   MappingTypeIdentityPolicy.Create(right) &&
               StringComparer.Ordinal.Equals(
                   BclTupleShapePolicy.BuildPresentationKey(left),
                   BclTupleShapePolicy.BuildPresentationKey(right));
    }
}
