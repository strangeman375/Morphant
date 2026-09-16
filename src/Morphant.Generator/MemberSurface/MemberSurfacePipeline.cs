using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
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
}
