using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.MappingPair;

internal static class CanonicalMappingPairPipeline
{
    public static IncrementalValuesProvider<CanonicalMappingPairCandidate>
        Build(
            IncrementalGeneratorInitializationContext context,
            IncrementalValuesProvider<MapperPairConfigurationModel>
                configurations)
    {
        var candidateCollections = GeneratorStageGuard.Select(
            context,
            configurations,
            "BuildCanonicalMappingPairCandidates",
            static (configuration, _) => BuildCandidates(configuration),
            static configuration => configuration.Declaration
                .AttributedDeclaration.Identifier.GetLocation());
        var candidates = candidateCollections.SelectMany(
            static (values, _) => values);
        var coordination = GeneratorStageGuard.Select(
                context,
                candidates.Collect(),
                "BuildCanonicalMappingPairCoordination",
                static (values, cancellationToken) =>
                    BuildCoordination(values, cancellationToken),
                new CanonicalPairCoordination(
                    ImmutableArray<string>.Empty))
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
        return BuildCandidates(
            configuration.MappingPairs.MapperIdentity,
            configuration.Declaration.MapperType,
            configuration.SurfaceMappingPairs,
            configuration.Declaration.Compilation);
    }

    internal static ImmutableArray<CanonicalMappingPairCandidate>
        BuildCandidates(
            string targetMapperIdentity,
            INamedTypeSymbol targetMapperType,
            ImmutableArray<MapperMappingPairModel> mapperModels,
            CSharpCompilation compilation)
    {
        var result =
            ImmutableArray.CreateBuilder<CanonicalMappingPairCandidate>();
        var identities = new HashSet<string>(StringComparer.Ordinal);

        for (var modelIndex = 0;
             modelIndex < mapperModels.Length;
             modelIndex++)
        {
            var mapperModel = mapperModels[modelIndex];
            var semanticModel = compilation.GetSemanticModel(
                mapperModel.ConfigureSyntax.SyntaxTree);

            if (mapperModel.ConfigureSyntax.Parent is not
                    TypeDeclarationSyntax declaration ||
                semanticModel.GetDeclaredSymbol(declaration) is not
                    INamedTypeSymbol declaringMapperType)
            {
                continue;
            }

            var constructedDeclaringMapperType =
                FindConstructedMapperType(
                    targetMapperType,
                    declaringMapperType,
                    out var declaringMapperDepth);

            if (constructedDeclaringMapperType is null)
            {
                continue;
            }

            var substitutions = MapperTypeSubstitution.Build(
                declaringMapperType,
                constructedDeclaringMapperType);

            for (var pairIndex = 0;
                 pairIndex < mapperModel.Pairs.Length;
                 pairIndex++)
            {
                var pair = mapperModel.Pairs[pairIndex];
                var candidateIdentity = BuildCandidateIdentity(
                    targetMapperIdentity,
                    mapperModel.MapperIdentity,
                    pair);

                if (identities.Add(candidateIdentity))
                {
                    result.Add(
                        new CanonicalMappingPairCandidate(
                            candidateIdentity,
                            targetMapperIdentity,
                            targetMapperType,
                            MapperTypeSubstitution.Substitute(
                                pair.SourceType,
                                substitutions,
                                compilation),
                            MapperTypeSubstitution.Substitute(
                                pair.DestinationType,
                                substitutions,
                                compilation),
                            declaringMapperDepth,
                            pair,
                            MappingSurfacePolicy.Create(
                                pair,
                                declaringMapperType),
                            compilation));
                }
            }
        }

        return result.ToImmutable();
    }

    private static CanonicalPairCoordination BuildCoordination(
        ImmutableArray<CanonicalMappingPairCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var effective = new Dictionary<
            string,
            CanonicalMappingPairCandidate>(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = candidate.TargetMapperIdentity + "|" +
                MappingTypeIdentityPolicy
                .CreateAlphaEquivalentPairKey(
                    candidate.EffectiveSourceType,
                    candidate.EffectiveDestinationType);

            if (!effective.TryGetValue(key, out var current) ||
                CompareForTarget(candidate, current) < 0)
            {
                effective[key] = candidate;
            }
        }

        var selected = new Dictionary<
            string,
            CanonicalMappingPairCandidate>(StringComparer.Ordinal);

        foreach (var candidate in effective.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = MappingTypeIdentityPolicy
                    .CreateAlphaEquivalentPairKey(
                        candidate.Pair.SourceType,
                        candidate.Pair.DestinationType) + "|" +
                candidate.Surface.CoordinationIdentity;

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

    internal static ImmutableArray<CanonicalMappingPairCandidate>
        SelectCandidates(
            ImmutableArray<CanonicalMappingPairCandidate> candidates,
            CancellationToken cancellationToken)
    {
        var coordination = BuildCoordination(
            candidates,
            cancellationToken);

        return candidates
            .Where(candidate => coordination.Contains(
                candidate.CandidateIdentity))
            .ToImmutableArray();
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

    private static int CompareForTarget(
        CanonicalMappingPairCandidate left,
        CanonicalMappingPairCandidate right)
    {
        var depthComparison = right.DeclaringMapperDepth.CompareTo(
            left.DeclaringMapperDepth);

        return depthComparison != 0
            ? depthComparison
            : Compare(left, right);
    }

    private static INamedTypeSymbol? FindConstructedMapperType(
        INamedTypeSymbol targetMapperType,
        INamedTypeSymbol declaringMapperType,
        out int depth)
    {
        depth = 0;

        for (var current = targetMapperType;
             current is not null;
             current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    current.OriginalDefinition,
                    declaringMapperType.OriginalDefinition))
            {
                return current;
            }

            depth++;
        }

        return null;
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
    string TargetMapperIdentity,
    INamedTypeSymbol TargetMapperType,
    ITypeSymbol EffectiveSourceType,
    ITypeSymbol EffectiveDestinationType,
    int DeclaringMapperDepth,
    MappingPairModel Pair,
    MappingSurfaceModel Surface,
    CSharpCompilation Compilation);
