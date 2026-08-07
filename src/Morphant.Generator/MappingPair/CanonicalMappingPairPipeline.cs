using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.MappingPair;

internal static class CanonicalMappingPairPipeline
{
    public static IncrementalValuesProvider<CanonicalMappingPairCandidate>
        Build(
            IncrementalValuesProvider<MapperPairConfigurationModel>
                configurations)
    {
        var candidates = configurations.SelectMany(
            static (configuration, _) => BuildCandidates(configuration));
        var coordination = candidates
            .Collect()
            .Select(static (values, cancellationToken) =>
                BuildCoordination(values, cancellationToken))
            .WithComparer(CanonicalPairCoordinationComparer.Instance);

        return candidates
            .Combine(coordination)
            .Select(static (source, _) =>
                source.Right.Contains(source.Left.CandidateIdentity)
                    ? source.Left
                    : (CanonicalMappingPairCandidate?)null)
            .WhereHasValue();
    }

    private static ImmutableArray<CanonicalMappingPairCandidate>
        BuildCandidates(MapperPairConfigurationModel configuration)
    {
        var result =
            ImmutableArray.CreateBuilder<CanonicalMappingPairCandidate>();
        var identities = new HashSet<string>(StringComparer.Ordinal);

        for (var modelIndex = 0;
             modelIndex < configuration.SurfaceMappingPairs.Length;
             modelIndex++)
        {
            var mapperModel =
                configuration.SurfaceMappingPairs[modelIndex];

            for (var pairIndex = 0;
                 pairIndex < mapperModel.Pairs.Length;
                 pairIndex++)
            {
                var pair = mapperModel.Pairs[pairIndex];
                var candidateIdentity = BuildCandidateIdentity(
                    configuration.MappingPairs.MapperIdentity,
                    mapperModel.MapperIdentity,
                    pair);

                if (identities.Add(candidateIdentity))
                {
                    result.Add(
                    new CanonicalMappingPairCandidate(
                            candidateIdentity,
                            pair));
                }
            }
        }

        return result.ToImmutable();
    }

    private static CanonicalPairCoordination BuildCoordination(
        ImmutableArray<CanonicalMappingPairCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var selected = new Dictionary<
            string,
            CanonicalMappingPairCandidate>(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = MappingTypeIdentityPolicy
                .CreateAlphaEquivalentPairKey(
                    candidate.Pair.SourceType,
                    candidate.Pair.DestinationType);

            if (!selected.TryGetValue(key, out var current) ||
                Compare(candidate, current) < 0)
            {
                selected[key] = candidate;
            }
        }

        return new CanonicalPairCoordination(
            selected.Values
                .Select(static candidate => candidate.CandidateIdentity)
                .OrderBy(static identity => identity, StringComparer.Ordinal)
                .ToImmutableArray());
    }

    private static int Compare(
        CanonicalMappingPairCandidate left,
        CanonicalMappingPairCandidate right)
    {
        var comparison = CanonicalMappingPairSelector.CompareRepresentation(
            left.Pair,
            right.Pair);

        return comparison != 0
            ? comparison
            : StringComparer.Ordinal.Compare(
                left.CandidateIdentity,
                right.CandidateIdentity);
    }

    private static string BuildCandidateIdentity(
        string targetMapperIdentity,
        string declaringMapperIdentity,
        MappingPairModel pair)
    {
        return targetMapperIdentity + "|" +
               declaringMapperIdentity + "|" +
               pair.SourceType.ToDisplayString(
                   SymbolDisplayFormats.FullyQualifiedNullable) + "|" +
               pair.DestinationType.ToDisplayString(
                   SymbolDisplayFormats.FullyQualifiedNullable);
    }

    private readonly record struct CanonicalPairCoordination(
        ImmutableArray<string> CandidateIdentities)
    {
        public bool Contains(string identity)
        {
            return CandidateIdentities.BinarySearch(
                       identity,
                       StringComparer.Ordinal) >= 0;
        }
    }

    private sealed class CanonicalPairCoordinationComparer :
        IEqualityComparer<CanonicalPairCoordination>
    {
        public static CanonicalPairCoordinationComparer Instance { get; } =
            new();

        public bool Equals(
            CanonicalPairCoordination left,
            CanonicalPairCoordination right)
        {
            return left.CandidateIdentities.SequenceEqual(
                right.CandidateIdentities,
                StringComparer.Ordinal);
        }

        public int GetHashCode(CanonicalPairCoordination value)
        {
            var hash = 17;

            foreach (var identity in value.CandidateIdentities)
            {
                hash = unchecked(
                    hash * 31 +
                    StringComparer.Ordinal.GetHashCode(identity));
            }

            return hash;
        }
    }
}

internal readonly record struct CanonicalMappingPairCandidate(
    string CandidateIdentity,
    MappingPairModel Pair);
