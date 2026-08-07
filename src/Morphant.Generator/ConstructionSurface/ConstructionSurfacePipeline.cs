using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.ConstructionSurface.ConstructionPlan;
using Morphant.Generator.ConstructionSurface.PairConfiguration;
using Morphant.Generator.Incrementality;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.ConstructionSurface;

internal static class ConstructionSurfacePipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValuesProvider<CanonicalMappingPairCandidate>
            canonicalPairs)
    {
        var planModels = ConstructionPlanPipeline.BuildModels(
            compilationContext,
            canonicalPairs);
        var planRequests = planModels
            .Select(static (model, _) =>
                new ConstructionSurfaceRequest(
                    model.HintName,
                    ConstructionPlanEmitter.Emit(model.Model)))
            .WithTrackingName(
                MorphantGeneratorStageNames
                    .BuildConstructionPlanRequests);
        var extensionModels = canonicalPairs
            .Combine(compilationContext)
            .Select(static (source, _) =>
                BuildPairConfigurationModel(
                    source.Left,
                    source.Right.Compilation))
            .WithComparer(MappingExtensionModelResultComparer.Instance)
            .WithTrackingName(
                MorphantGeneratorStageNames
                    .BuildMappingExtensionModels);
        var extensionHintNameAllocations = extensionModels
            .Select(static (model, _) =>
                new HintNameIdentity(
                    model.StableIdentity,
                    HintNameHelper.ToHintNamePart(
                        model.StableIdentity)))
            .Collect()
            .Select(static (identities, cancellationToken) =>
                HintNameCollisions.Build(
                    identities,
                    cancellationToken))
            .WithComparer(HintNameAllocationsComparer.Instance);
        var extensionRequests = extensionModels
            .Combine(extensionHintNameAllocations)
            .Select(static (source, _) =>
                new ConstructionSurfaceRequest(
                    GeneratedSourceHintName.Create(
                        "MappingExtension",
                        HintNameCollisions.Resolve(
                            source.Right,
                            source.Left.StableIdentity)),
                    PairConfigurationEmitter.Emit(source.Left.Model)))
            .WithTrackingName(
                MorphantGeneratorStageNames
                    .BuildMappingExtensionRequests);

        context.RegisterSourceOutput(
            planRequests,
            AddSource);
        context.RegisterSourceOutput(
            extensionRequests,
            AddSource);
    }

    private static void AddSource(
        SourceProductionContext sourceProductionContext,
        ConstructionSurfaceRequest request)
    {
        sourceProductionContext.AddSource(
            request.HintName,
            SourceText.From(request.Source, Encoding.UTF8));
    }

    private static MappingExtensionModelResult
        BuildPairConfigurationModel(
            CanonicalMappingPairCandidate candidate,
            Compilation compilation)
    {
        var pair = candidate.Pair;
        var stableIdentity =
            RemoveGlobalAlias(pair.Identity.Source.DisplayName) +
            "__" +
            RemoveGlobalAlias(pair.Identity.Destination.DisplayName);

        return new MappingExtensionModelResult(
            candidate.CandidateIdentity,
            stableIdentity,
            PairConfigurationModelBuilder.Build(pair, compilation));
    }

    private sealed class MappingExtensionModelResultComparer :
        IEqualityComparer<MappingExtensionModelResult>
    {
        public static MappingExtensionModelResultComparer Instance { get; } =
            new();

        public bool Equals(
            MappingExtensionModelResult left,
            MappingExtensionModelResult right)
        {
            return StringComparer.Ordinal.Equals(
                       left.StableIdentity,
                       right.StableIdentity) &&
                   PairConfigurationModelEquality.Equal(
                       left.Model,
                       right.Model);
        }

        public int GetHashCode(MappingExtensionModelResult value)
        {
            return StringComparer.Ordinal.GetHashCode(value.HintName);
        }
    }

    private readonly record struct MappingExtensionModelResult(
        string CandidateIdentity,
        string StableIdentity,
        PairConfigurationModel Model)
    {
        public string HintName => GeneratedSourceHintName.Create(
            "MappingExtension",
            HintNameHelper.ToHintNamePart(StableIdentity));
    }

    internal static ImmutableArray<ConstructionSurfaceRequest> BuildRequests(
        ImmutableArray<MapperMappingPairModel> mapperModels,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var pairs = CanonicalMappingPairSelector.Select(
            mapperModels,
            cancellationToken);
        var requests =
            ImmutableArray.CreateBuilder<ConstructionSurfaceRequest>();

        AddConstructionPlanRequests(
            pairs,
            compilation,
            requests,
            cancellationToken);
        AddPairConfigurationRequests(
            pairs,
            compilation,
            requests,
            cancellationToken);

        return requests.ToImmutable();
    }

    private static void AddConstructionPlanRequests(
        ImmutableArray<MappingPairModel> pairs,
        Compilation compilation,
        ImmutableArray<ConstructionSurfaceRequest>.Builder requests,
        CancellationToken cancellationToken)
    {
        var definitions =
            new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);

        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!pair.Capabilities.StructuredConstruction)
            {
                continue;
            }

            var destination =
                DestinationCapabilityPolicy.GetDestinationType(
                    pair.DestinationType,
                    compilation);
            var definition = destination.OriginalDefinition;
            var identity = definition.ContainingAssembly.Identity + "|" +
                           SymbolNameHelper.GetFullMetadataName(definition);

            if (!definitions.ContainsKey(identity))
            {
                definitions.Add(identity, definition);
            }
        }

        var hintNameAllocator = new HintNamePartAllocator();

        foreach (var definition in definitions.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadataName =
                SymbolNameHelper.GetFullMetadataName(definition.Value);
            var planNamespace =
                GeneratedPlanNaming.BuildNamespace(
                    definition.Value);
            var planTypeName =
                GeneratedPlanNaming.BuildConstructionTypeName(
                    definition.Value);
            var model = ConstructionPlanModelBuilder.Build(
                definition.Value,
                planNamespace,
                planTypeName,
                compilation,
                cancellationToken);
            var hintName = GeneratedSourceHintName.Create(
                "Construction",
                hintNameAllocator.Allocate(metadataName));

            requests.Add(
                new ConstructionSurfaceRequest(
                    hintName,
                    ConstructionPlanEmitter.Emit(model)));
        }
    }

    private static void AddPairConfigurationRequests(
        ImmutableArray<MappingPairModel> pairs,
        Compilation compilation,
        ImmutableArray<ConstructionSurfaceRequest>.Builder requests,
        CancellationToken cancellationToken)
    {
        var hintNameAllocator = new HintNamePartAllocator();

        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stableIdentity =
                RemoveGlobalAlias(pair.Identity.Source.DisplayName) +
                "__" +
                RemoveGlobalAlias(pair.Identity.Destination.DisplayName);
            var hintName = GeneratedSourceHintName.Create(
                "MappingExtension",
                hintNameAllocator.Allocate(stableIdentity));
            var model = PairConfigurationModelBuilder.Build(
                pair,
                compilation);

            requests.Add(
                new ConstructionSurfaceRequest(
                    hintName,
                    PairConfigurationEmitter.Emit(model)));
        }
    }

    private static string RemoveGlobalAlias(string value)
    {
        return value.Replace("global::", string.Empty);
    }

    internal readonly record struct ConstructionSurfaceRequest(
        string HintName,
        string Source);
}
