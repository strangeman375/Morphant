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
}
