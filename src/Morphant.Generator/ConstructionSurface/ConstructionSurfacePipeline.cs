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
        IncrementalValuesProvider<CanonicalMappingPairCandidate>
            canonicalPairs)
    {
        var planModels = ConstructionPlanPipeline.BuildModels(
            context,
            canonicalPairs);
        var planRequests = GeneratorStageGuard.SelectTrackedSourceRequest(
                context,
                planModels,
                MorphantGeneratorStageNames.BuildConstructionPlanRequests,
                static (model, _) =>
                    new ConstructionSurfaceRequest(
                        model.HintName,
                        ConstructionPlanEmitter.Emit(model.Model)),
                static _ => Location.None);
        var extensionModels = GeneratorStageGuard.Select(
                context,
                canonicalPairs,
                MorphantGeneratorStageNames.BuildMappingExtensionModels,
                static (candidate, _) =>
                    BuildPairConfigurationModel(
                        candidate,
                        candidate.Compilation),
                static candidate =>
                    candidate.Pair.Registration.Syntax.GetLocation())
            .WithComparer(MappingExtensionModelResultComparer.Instance)
            .WithTrackingName(
                MorphantGeneratorStageNames
                    .BuildMappingExtensionModels);
        var extensionHintNameIdentities = extensionModels
            .Select(static (model, _) =>
                new HintNameIdentity(
                    model.StableIdentity,
                    HintNameHelper.ToHintNamePart(
                        model.StableIdentity)));
        var extensionHintNameAllocations = GeneratorStageGuard.Select(
                context,
                extensionHintNameIdentities.Collect(),
                "AllocateMappingExtensionHintNames",
                static (identities, cancellationToken) =>
                    HintNameCollisions.Build(
                        identities,
                        cancellationToken),
                new HintNameAllocations(
                    ImmutableArray<HintNameAllocation>.Empty))
            .WithComparer(HintNameAllocationsComparer.Instance);
        var extensionRequests =
            GeneratorStageGuard.SelectTrackedSourceRequest(
                context,
                extensionModels.Combine(extensionHintNameAllocations),
                MorphantGeneratorStageNames.BuildMappingExtensionRequests,
                static (source, _) =>
                    new ConstructionSurfaceRequest(
                        GeneratedSourceHintName.Create(
                            "MappingExtension",
                            HintNameCollisions.Resolve(
                                source.Right,
                                source.Left.StableIdentity)),
                        PairConfigurationEmitter.Emit(source.Left.Model)),
                static _ => Location.None);

        GeneratorStageGuard.RegisterSourceOutput(
            context,
            planRequests,
            "AddConstructionPlanSource",
            static request => request.HintName,
            AddSource);
        GeneratorStageGuard.RegisterSourceOutput(
            context,
            extensionRequests,
            "AddMappingExtensionSource",
            static request => request.HintName,
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
        var stableIdentity = BuildExtensionStableIdentity(candidate);

        return new MappingExtensionModelResult(
            candidate.CandidateIdentity,
            stableIdentity,
            PairConfigurationModelBuilder.Build(
                pair,
                candidate.Surface,
                compilation));
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
        ImmutableArray<CanonicalMappingPairCandidate> candidates,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var requests =
            ImmutableArray.CreateBuilder<ConstructionSurfaceRequest>();
        var pairs = candidates
            .Select(static candidate => candidate.Pair)
            .ToImmutableArray();

        AddConstructionPlanRequests(
            pairs,
            compilation,
            requests,
            cancellationToken);
        AddPairConfigurationRequests(
            candidates,
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
            new Dictionary<string, ConstructionPlanDefinition>(
                StringComparer.Ordinal);

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
            var tuple = BclTupleShapePolicy.TryCreate(destination);
            var definition = tuple is null
                ? destination.OriginalDefinition
                : destination;
            var identity = tuple is null
                ? definition.ContainingAssembly.Identity + "|" +
                  SymbolNameHelper.GetFullMetadataName(definition)
                : "tuple|" +
                  BclTuplePlanNaming.BuildStableIdentity(tuple);

            if (!definitions.ContainsKey(identity))
            {
                definitions.Add(
                    identity,
                    new ConstructionPlanDefinition(
                        definition,
                        tuple));
            }
        }

        var hintNameAllocator = new HintNamePartAllocator();

        foreach (var definition in definitions.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadataName = definition.Value.Tuple is { } tuple
                ? "Tuple." +
                  BclTuplePlanNaming.BuildHintIdentity(tuple)
                : SymbolNameHelper.GetFullMetadataName(
                    definition.Value.DestinationType);
            var model = definition.Value.Tuple is { } tupleShape
                ? BclTuplePlanModelBuilder.BuildConstruction(
                    tupleShape,
                    compilation)
                : ConstructionPlanModelBuilder.Build(
                    definition.Value.DestinationType,
                    GeneratedPlanNaming.BuildNamespace(
                        definition.Value.DestinationType),
                    GeneratedPlanNaming.BuildConstructionTypeName(
                        definition.Value.DestinationType),
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
        ImmutableArray<CanonicalMappingPairCandidate> candidates,
        Compilation compilation,
        ImmutableArray<ConstructionSurfaceRequest>.Builder requests,
        CancellationToken cancellationToken)
    {
        var hintNameAllocator = new HintNamePartAllocator();

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stableIdentity = BuildExtensionStableIdentity(candidate);
            var hintName = GeneratedSourceHintName.Create(
                "MappingExtension",
                hintNameAllocator.Allocate(stableIdentity));
            var model = PairConfigurationModelBuilder.Build(
                candidate.Pair,
                candidate.Surface,
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

    private static string BuildExtensionStableIdentity(
        CanonicalMappingPairCandidate candidate)
    {
        var pair = candidate.Pair;
        var pairIdentity =
            RemoveGlobalAlias(pair.Identity.Source.DisplayName) +
            "__" +
            RemoveGlobalAlias(pair.Identity.Destination.DisplayName);

        return candidate.Surface.Kind == MappingSurfaceKind.Shared
            ? pairIdentity
            : pairIdentity + "__" +
              RemoveGlobalAlias(
                  candidate.Surface.ReadableScopeIdentity);
    }

    internal readonly record struct ConstructionSurfaceRequest(
        string HintName,
        string Source) : IGeneratedSourceRequest;

    private readonly record struct ConstructionPlanDefinition(
        INamedTypeSymbol DestinationType,
        BclTupleShape? Tuple);
}
