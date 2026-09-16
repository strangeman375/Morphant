using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.ConstructionSurface.ConstructionPlan;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.ConstructionSurface;

internal static class ConstructionSurfacePipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<CanonicalMappingPairCandidate>
            canonicalPairs,
        IncrementalValuesProvider<MappingExtensionModelResult> extensionModels)
    {
        var planModels = ConstructionPlanPipeline.BuildModels(
            context,
            canonicalPairs);
        var planRequests = GeneratorStageGuard.SelectTrackedSourceRequest(
                context,
                planModels,
                MorphantGeneratorStageNames.BuildConstructionPlanRequests,
                static (model, _) =>
                    new DslSurfaceRequest(
                        model.HintName,
                        ConstructionPlanEmitter.Emit(model.Model)),
                static _ => Location.None);
        GeneratorStageGuard.RegisterSourceOutput(
            context,
            planRequests,
            "AddConstructionPlanSource",
            static request => request.HintName,
            AddSource);
        MappingExtensionPipeline.RegisterConstruction(context, extensionModels);
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
        AddConstructionPlanRequests(
            candidates.Select(static candidate => candidate.Pair).ToImmutableArray(),
            compilation,
            requests,
            cancellationToken);
        return requests.ToImmutable();
    }

    private static void AddConstructionPlanRequests(
        ImmutableArray<MappingPairModel> pairs,
        Compilation compilation,
        ImmutableArray<DslSurfaceRequest>.Builder requests,
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

        foreach (var definition in definitions.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var model = definition.Value.Tuple is { } tupleShape
                ? BclTuplePlanModelBuilder.BuildConstruction(
                    tupleShape,
                    compilation)
                : ConstructionPlanModelBuilder.Build(
                    definition.Value.DestinationType,
                    GeneratedPlanNaming.BuildNamespace(
                        definition.Value.DestinationType,
                        compilation),
                    GeneratedPlanNaming.BuildConstructionTypeName(
                        definition.Value.DestinationType),
                    compilation,
                    cancellationToken);
            var hintName = GeneratedSourceHintName.ForDestination(
                "Construction",
                definition.Value.DestinationType,
                compilation);

            requests.Add(
                new DslSurfaceRequest(
                    hintName,
                    ConstructionPlanEmitter.Emit(model)));
        }
    }

    private readonly record struct ConstructionPlanDefinition(
        INamedTypeSymbol DestinationType,
        BclTupleShape? Tuple);
}
