using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.ConstructionSurface.PairConfiguration;
using Morphant.Generator.Incrementality;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MemberSurface.MemberPlan;
using Morphant.Generator.MemberSurface.PairConfiguration;

namespace Morphant.Generator.MemberSurface;

internal static class MemberSurfacePipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValuesProvider<CanonicalMappingPairCandidate>
            canonicalPairs)
    {
        var planModels = MemberPlanPipeline.BuildModels(
            compilationContext,
            canonicalPairs);
        var planRequests = planModels
            .Select(static (model, _) =>
                new MemberSurfaceRequest(
                    model.HintName,
                    MemberPlanEmitter.Emit(model.Model)))
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildMemberPlanRequests);
        var extensionModels = canonicalPairs
            .Where(static candidate => candidate.Pair.Capabilities.Members)
            .Combine(compilationContext)
            .Select(static (source, _) =>
                BuildPairConfigurationModel(
                    source.Left,
                    source.Right.Compilation))
            .WithComparer(MemberExtensionModelResultComparer.Instance)
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildMemberExtensionModels);
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
                new MemberSurfaceRequest(
                    GeneratedSourceHintName.Create(
                        "MemberExtension",
                        HintNameCollisions.Resolve(
                            source.Right,
                            source.Left.StableIdentity)),
                    MemberConfigurationEmitter.Emit(source.Left.Model)))
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildMemberExtensionRequests);

        context.RegisterSourceOutput(
            planRequests,
            AddSource);
        context.RegisterSourceOutput(
            extensionRequests,
            AddSource);
    }

    private static void AddSource(
        SourceProductionContext sourceProductionContext,
        MemberSurfaceRequest request)
    {
        sourceProductionContext.AddSource(
            request.HintName,
            SourceText.From(request.Source, Encoding.UTF8));
    }

    private static MemberExtensionModelResult BuildPairConfigurationModel(
        CanonicalMappingPairCandidate candidate,
        Compilation compilation)
    {
        var pair = candidate.Pair;
        var stableIdentity =
            RemoveGlobalAlias(pair.Identity.Source.DisplayName) +
            "__" +
            RemoveGlobalAlias(pair.Identity.Destination.DisplayName);

        return new MemberExtensionModelResult(
            candidate.CandidateIdentity,
            stableIdentity,
            PairConfigurationModelBuilder.Build(pair, compilation));
    }

    private sealed class MemberExtensionModelResultComparer :
        IEqualityComparer<MemberExtensionModelResult>
    {
        public static MemberExtensionModelResultComparer Instance { get; } =
            new();

        public bool Equals(
            MemberExtensionModelResult left,
            MemberExtensionModelResult right)
        {
            return StringComparer.Ordinal.Equals(
                       left.StableIdentity,
                       right.StableIdentity) &&
                   PairConfigurationModelEquality.Equal(
                       left.Model,
                       right.Model);
        }

        public int GetHashCode(MemberExtensionModelResult value)
        {
            return StringComparer.Ordinal.GetHashCode(value.HintName);
        }
    }

    private readonly record struct MemberExtensionModelResult(
        string CandidateIdentity,
        string StableIdentity,
        PairConfigurationModel Model)
    {
        public string HintName => GeneratedSourceHintName.Create(
            "MemberExtension",
            HintNameHelper.ToHintNamePart(StableIdentity));
    }

    internal static ImmutableArray<MemberSurfaceRequest> BuildRequests(
        ImmutableArray<MappingPairModel> pairs,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var requests = ImmutableArray.CreateBuilder<MemberSurfaceRequest>();

        AddMemberPlanRequests(
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

    private static void AddMemberPlanRequests(
        ImmutableArray<MappingPairModel> pairs,
        Compilation compilation,
        ImmutableArray<MemberSurfaceRequest>.Builder requests,
        CancellationToken cancellationToken)
    {
        var definitions =
            new Dictionary<string, MemberPlanDefinition>(
                StringComparer.Ordinal);

        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!pair.Capabilities.Members)
            {
                continue;
            }

            var destination = DestinationCapabilityPolicy
                .GetDestinationType(
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
                    new MemberPlanDefinition(
                        definition,
                        pair.Capabilities.StructuredConstruction,
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
                ? BclTuplePlanModelBuilder.BuildMembers(
                    tupleShape,
                    compilation)
                : MemberPlanModelBuilder.Build(
                    definition.Value.DestinationType,
                    definition.Value.IncludeInitOnlyProperties,
                    compilation,
                    cancellationToken);
            var hintName = GeneratedSourceHintName.Create(
                "Member",
                hintNameAllocator.Allocate(metadataName));

            requests.Add(
                new MemberSurfaceRequest(
                    hintName,
                    MemberPlanEmitter.Emit(model)));
        }
    }

    private static void AddPairConfigurationRequests(
        ImmutableArray<MappingPairModel> pairs,
        Compilation compilation,
        ImmutableArray<MemberSurfaceRequest>.Builder requests,
        CancellationToken cancellationToken)
    {
        var hintNameAllocator = new HintNamePartAllocator();

        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!pair.Capabilities.Members)
            {
                continue;
            }

            var stableIdentity =
                RemoveGlobalAlias(pair.Identity.Source.DisplayName) +
                "__" +
                RemoveGlobalAlias(pair.Identity.Destination.DisplayName);
            var hintName = GeneratedSourceHintName.Create(
                "MemberExtension",
                hintNameAllocator.Allocate(stableIdentity));
            var model = PairConfigurationModelBuilder.Build(
                pair,
                compilation);

            requests.Add(
                new MemberSurfaceRequest(
                    hintName,
                    MemberConfigurationEmitter.Emit(model)));
        }
    }

    private static string RemoveGlobalAlias(string value)
    {
        return value.Replace("global::", string.Empty);
    }

    private readonly record struct MemberPlanDefinition(
        INamedTypeSymbol DestinationType,
        bool IncludeInitOnlyProperties,
        BclTupleShape? Tuple);

    internal readonly record struct MemberSurfaceRequest(
        string HintName,
        string Source);
}
