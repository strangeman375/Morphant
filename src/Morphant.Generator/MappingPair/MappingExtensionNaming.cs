using System.Globalization;
using Microsoft.CodeAnalysis;
using Morphant.Generator.ConstructionSurface;

namespace Morphant.Generator.MappingPair;

internal static class MappingExtensionNaming
{
    public static string BuildHintName(
        string artifactKind,
        CanonicalMappingPairCandidate candidate)
    {
        var pair = candidate.Pair;
        // Global type contracts omit dependency versions and facade identities.
        // Presentation restores tuple names, nullability and dynamic, which the
        // normalized mapping contract intentionally does not distinguish.
        var parameters = GeneratedTypeNameBuilder.CollectTypeParameters(pair.SourceType, pair.DestinationType);
        Dictionary<ITypeParameterSymbol, string>? names = null;
        if (!parameters.IsEmpty)
        {
            names = new(SymbolEqualityComparer.Default);
            for (var index = 0; index < parameters.Length; index++)
            {
                names.Add(parameters[index], index == 0 ? "T" : "T" + (index + 1).ToString(CultureInfo.InvariantCulture));
            }
        }

        var identity = GeneratedEntityIdentity.Combine(
            names is null ? pair.Identity.Source.DisplayName : MappingTypeIdentityPolicy.Create(pair.SourceType, names).DisplayName,
            names is null ? pair.Identity.Destination.DisplayName : MappingTypeIdentityPolicy.Create(pair.DestinationType, names).DisplayName,
            BclTupleShapePolicy.BuildPairPresentationKey(
                pair.SourceType,
                pair.DestinationType));
        var label = GeneratedSourceHintName.BuildTypeLabel(pair.SourceType, names) +
                    "To" +
                    GeneratedSourceHintName.BuildTypeLabel(pair.DestinationType, names);

        return GeneratedSourceHintName.Create(
            artifactKind,
            label,
            GeneratedEntityIdentity.Create("pair:" + identity, candidate.Compilation));
    }
}
