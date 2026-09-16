using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MemberSurface.MemberPlan;

namespace Morphant.Generator.MemberSurface;

internal static class MemberSurfacePipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<CanonicalMappingPairCandidate>
            canonicalPairs,
        IncrementalValuesProvider<MappingExtensionModelResult> extensionModels)
    {
        var planModels = MemberPlanPipeline.BuildModels(
            context,
            canonicalPairs);
        var planRequests = GeneratorStageGuard.SelectTrackedSourceRequest(
                context,
                planModels,
                MorphantGeneratorStageNames.BuildMemberPlanRequests,
                static (model, _) =>
                    new DslSurfaceRequest(
                        model.HintName,
                        MemberPlanEmitter.Emit(model.Model)),
                static _ => Location.None);
        GeneratorStageGuard.RegisterSourceOutput(
            context,
            planRequests,
            "AddMemberPlanSource",
            static request => request.HintName,
            AddSource);
        MappingExtensionPipeline.RegisterMembers(context, extensionModels);
    }

    private static void AddSource(
        SourceProductionContext sourceProductionContext,
        DslSurfaceRequest request)
    {
        sourceProductionContext.AddSource(
            request.HintName,
            SourceText.From(request.Source, Encoding.UTF8));
    }

    internal static ImmutableArray<DslSurfaceRequest> BuildPlanRequests(
        ImmutableArray<CanonicalMappingPairCandidate> candidates,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var requests = ImmutableArray.CreateBuilder<DslSurfaceRequest>();
        AddMemberPlanRequests(
            candidates.Select(static candidate => candidate.Pair).ToImmutableArray(),
            compilation,
            requests,
            cancellationToken);
        return requests.ToImmutable();
    }

    private static void AddMemberPlanRequests(
        ImmutableArray<MappingPairModel> pairs,
        Compilation compilation,
        ImmutableArray<DslSurfaceRequest>.Builder requests,
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
                new DslSurfaceRequest(
                    hintName,
                    MemberPlanEmitter.Emit(model)));
        }
    }

    private readonly record struct MemberPlanDefinition(
        INamedTypeSymbol DestinationType,
        bool IncludeInitOnlyProperties,
        BclTupleShape? Tuple);
}
