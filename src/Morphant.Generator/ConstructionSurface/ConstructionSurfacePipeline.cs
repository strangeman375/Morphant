using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        var fileScopedNamespace = context.ParseOptionsProvider.Select(
            static (options, _) => ((CSharpParseOptions)options).LanguageVersion >= LanguageVersion.CSharp10);
        var planRequests = GeneratorStageGuard.SelectTrackedSourceRequest(
                context,
                planModels.Combine(fileScopedNamespace),
                MorphantGeneratorStageNames.BuildConstructionPlanRequests,
                static (model, _) =>
                    new DslSurfaceRequest(
                        model.Left.HintName,
                        ConstructionPlanEmitter.Emit(model.Left.Model, model.Right)),
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
