using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class MappingExtensionNaming
{
    public static string BuildHintName(
        string artifactKind,
        CanonicalMappingPairCandidate candidate)
    {
        var surface = candidate.Surface;
        var pair = candidate.Pair;
        // Global type contracts omit dependency versions and facade identities.
        // Presentation restores tuple names, nullability and dynamic, which the
        // normalized mapping contract intentionally does not distinguish.
        var identity = GeneratedEntityIdentity.Combine(
            surface.Kind.ToString(),
            SymbolNameHelper.GetFullMetadataName(
                surface.DeclaringMapperType.OriginalDefinition),
            surface.ReadableScopeIdentity,
            pair.Identity.Source.DisplayName,
            pair.Identity.Destination.DisplayName,
            BclTupleShapePolicy.BuildPairPresentationKey(
                pair.SourceType,
                pair.DestinationType));
        var label = HintNameHelper.ToHintNamePart(
                        surface.DeclaringMapperType.Name) + "." +
                    GeneratedSourceHintName.BuildTypeLabel(pair.SourceType) +
                    "To" +
                    GeneratedSourceHintName.BuildTypeLabel(pair.DestinationType);

        return GeneratedSourceHintName.Create(
            artifactKind,
            label,
            GeneratedEntityIdentity.Create("pair:" + identity, candidate.Compilation));
    }
}
