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
                new CanonicalPairCoordination(ImmutableSortedDictionary<string, bool>.Empty))
            .WithComparer(CanonicalPairCoordinationComparer.Instance);

        return candidates
            .Combine(coordination)
            .Select(static (source, _) => source.Right.Select(source.Left))
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
                    INamedTypeSymbol declaringMapperType ||
                HasFileLocalScope(declaringMapperType))
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
                            mapperModel.MapperIdentity,
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
                            compilation));
                }
            }
        }

        return result.ToImmutable();
    }

    private static bool HasFileLocalScope(INamedTypeSymbol mapperType)
    {
        for (var scope = mapperType; scope is not null; scope = scope.ContainingType)
        {
            if (scope.IsFileLocal)
            {
                return true;
            }
        }

        return false;
    }

    private static CanonicalPairCoordination BuildCoordination(
        ImmutableArray<CanonicalMappingPairCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var effective = new Dictionary<
            string,
            CanonicalMappingPairCandidate>(StringComparer.Ordinal);

        var needsLocalExtension = new HashSet<string>(StringComparer.Ordinal);
        var owners = new Dictionary<string, CanonicalMappingPairCandidate>(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = candidate.TargetMapperIdentity + "|" +
                MappingTypeIdentityPolicy
                .CreateAlphaEquivalentPairKey(
                    candidate.EffectiveSourceType,
                    candidate.EffectiveDestinationType) + "|" +
                BclTupleShapePolicy.BuildPairPresentationKey(
                    candidate.EffectiveSourceType,
                    candidate.EffectiveDestinationType);

            if (!effective.TryGetValue(key, out var current) ||
                CompareForTarget(candidate, current) < 0)
            {
                effective[key] = candidate;
            }

            var declaredKey = candidate.DeclaredPairKey;
            if (candidate.Pair.FriendAssembly is null)
                needsLocalExtension.Add(declaredKey);
            if (!owners.TryGetValue(declaredKey, out var owner) || Compare(candidate, owner) < 0)
                owners[declaredKey] = candidate;
        }

        // Inheritance selects which signatures are needed. All declarations
        // of each selected signature take part in choosing its presentation.
        return new CanonicalPairCoordination(effective.Values
            .Select(static candidate => candidate.DeclaredPairKey).Distinct(StringComparer.Ordinal)
            .ToImmutableSortedDictionary(key => owners[key].CandidateIdentity, needsLocalExtension.Contains, StringComparer.Ordinal));
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
            .Select(coordination.Select)
            .Where(static candidate => candidate.HasValue)
            .Select(static candidate => candidate!.Value)
            .ToImmutableArray();
    }

    internal static readonly IComparer<CanonicalMappingPairCandidate> DeclarationOrder =
        Comparer<CanonicalMappingPairCandidate>.Create(Compare);

    private static int Compare(
        CanonicalMappingPairCandidate left,
        CanonicalMappingPairCandidate right)
    {
        var leftSyntax = left.Pair.Registration.Syntax;
        var rightSyntax = right.Pair.Registration.Syntax;
        var comparison = StringComparer.Ordinal.Compare(leftSyntax.SyntaxTree.FilePath, rightSyntax.SyntaxTree.FilePath);
        if (comparison == 0)
        {
            comparison = leftSyntax.SpanStart.CompareTo(rightSyntax.SpanStart);
        }

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
        ImmutableSortedDictionary<string, bool> Owners)
    {
        public CanonicalMappingPairCandidate? Select(CanonicalMappingPairCandidate candidate) =>
            Owners.TryGetValue(candidate.CandidateIdentity, out var needsLocal)
                ? candidate with { EmitLocalExtension = needsLocal }
                : null;
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
            return left.Owners.SequenceEqual(right.Owners);
        }

        public int GetHashCode(CanonicalPairCoordination value)
        {
            var hash = 17;

            foreach (var owner in value.Owners)
            {
                hash = unchecked(
                    hash * 31 +
                    owner.GetHashCode());
            }

            return hash;
        }
    }
}

internal readonly record struct CanonicalMappingPairCandidate(
    string CandidateIdentity,
    string TargetMapperIdentity,
    string DeclaringMapperIdentity,
    ITypeSymbol EffectiveSourceType,
    ITypeSymbol EffectiveDestinationType,
    int DeclaringMapperDepth,
    MappingPairModel Pair,
    CSharpCompilation Compilation,
    bool EmitLocalExtension = true)
{
    public string DeclaredPairKey => MappingTypeIdentityPolicy.CreateAlphaEquivalentPairKey(Pair.SourceType, Pair.DestinationType);
    public string EffectivePairKey => MappingTypeIdentityPolicy.CreateAlphaEquivalentPairKey(EffectiveSourceType, EffectiveDestinationType);
}
