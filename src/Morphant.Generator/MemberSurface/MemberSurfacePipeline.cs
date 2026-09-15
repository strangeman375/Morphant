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
        IncrementalValuesProvider<CanonicalMappingPairCandidate>
            canonicalPairs)
    {
        var planModels = MemberPlanPipeline.BuildModels(
            context,
            canonicalPairs);
        var planRequests = GeneratorStageGuard.SelectTrackedSourceRequest(
                context,
                planModels,
                MorphantGeneratorStageNames.BuildMemberPlanRequests,
                static (model, _) =>
                    new MemberSurfaceRequest(
                        model.HintName,
                        MemberPlanEmitter.Emit(model.Model)),
                static _ => Location.None);
        var extensionModels = GeneratorStageGuard.Select(
                context,
                canonicalPairs.Where(static candidate =>
                    candidate.Pair.Capabilities.Members),
                MorphantGeneratorStageNames.BuildMemberExtensionModels,
                static (candidate, _) =>
                    BuildPairConfigurationModel(
                        candidate,
                        candidate.Compilation),
                static candidate =>
                    candidate.Pair.Registration.Syntax.GetLocation())
            .WithComparer(MemberExtensionModelResultComparer.Instance)
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildMemberExtensionModels);
        var extensionRequests =
            GeneratorStageGuard.SelectTrackedSourceRequest(
                context,
                extensionModels,
                MorphantGeneratorStageNames.BuildMemberExtensionRequests,
                static (model, _) =>
                    new MemberSurfaceRequest(
                        model.HintName,
                        MemberConfigurationEmitter.Emit(model.Model)),
                static _ => Location.None);

        GeneratorStageGuard.RegisterSourceOutput(
            context,
            planRequests,
            "AddMemberPlanSource",
            static request => request.HintName,
            AddSource);
        GeneratorStageGuard.RegisterSourceOutput(
            context,
            extensionRequests,
            "AddMemberExtensionSource",
            static request => request.HintName,
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

        return new MemberExtensionModelResult(
            MappingExtensionNaming.BuildHintName("MemberExtension", candidate),
            PairConfigurationModelBuilder.Build(
                pair,
                candidate.Surface,
                compilation));
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
                       left.HintName,
                       right.HintName) &&
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
        string HintName,
        PairConfigurationModel Model);

    internal static ImmutableArray<MemberSurfaceRequest> BuildRequests(
        ImmutableArray<CanonicalMappingPairCandidate> candidates,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var requests = ImmutableArray.CreateBuilder<MemberSurfaceRequest>();
        var pairs = candidates
            .Select(static candidate => candidate.Pair)
            .ToImmutableArray();

        AddMemberPlanRequests(
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

        foreach (var definition in definitions.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var model = definition.Value.Tuple is { } tupleShape
                ? BclTuplePlanModelBuilder.BuildMembers(
                    tupleShape,
                    compilation)
                : MemberPlanModelBuilder.Build(
                    definition.Value.DestinationType,
                    definition.Value.IncludeInitOnlyProperties,
                    compilation,
                    cancellationToken);
            var hintName = GeneratedSourceHintName.ForDestination(
                "Member",
                definition.Value.DestinationType,
                compilation);

            requests.Add(
                new MemberSurfaceRequest(
                    hintName,
                    MemberPlanEmitter.Emit(model)));
        }
    }

    private static void AddPairConfigurationRequests(
        ImmutableArray<CanonicalMappingPairCandidate> candidates,
        Compilation compilation,
        ImmutableArray<MemberSurfaceRequest>.Builder requests,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!candidate.Pair.Capabilities.Members)
            {
                continue;
            }

            var hintName = MappingExtensionNaming.BuildHintName(
                "MemberExtension", candidate);
            var model = PairConfigurationModelBuilder.Build(
                candidate.Pair,
                candidate.Surface,
                compilation);

            requests.Add(
                new MemberSurfaceRequest(
                    hintName,
                    MemberConfigurationEmitter.Emit(model)));
        }
    }

    private readonly record struct MemberPlanDefinition(
        INamedTypeSymbol DestinationType,
        bool IncludeInitOnlyProperties,
        BclTupleShape? Tuple);

    internal readonly record struct MemberSurfaceRequest(
        string HintName,
        string Source) : IGeneratedSourceRequest;
}
