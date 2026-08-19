using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Morphant.Generator.Incrementality;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TypeMapperPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<MappingSettings> assemblySettings,
        IncrementalValuesProvider<MapperPairConfigurationModel>
            mapperConfigurations)
    {
        Register(
            context,
            assemblySettings,
            MapperContractPipeline.Build(mapperConfigurations));
    }

    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<MappingSettings> assemblySettings,
        IncrementalValuesProvider<MapperContractAnalysis> contractAnalyses)
    {
        var models = contractAnalyses
            .Combine(assemblySettings)
            .Select(static (source, cancellationToken) =>
                TypeMapperModelBuilder.TryBuild(
                    (
                        (
                            source.Left,
                            source.Left.Configuration.Declaration.Context
                        ),
                        source.Right
                    ),
                    cancellationToken))
            .WhereHasValue()
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildTypeMapperModels);

        RegisterDiagnostics(context, models);
        RegisterSources(context, models);
    }

    private static void RegisterDiagnostics(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<TypeMapperGenerationInput> models)
    {
        var diagnostics = models
            .Collect()
            .Select(static (inputs, cancellationToken) =>
                BuildDiagnostics(inputs, cancellationToken));

        DiagnosticPipeline.Register(context, diagnostics);
    }

    private static ImmutableArray<Diagnostic> BuildDiagnostics(
        ImmutableArray<TypeMapperGenerationInput> inputs,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<Diagnostic>();

        result.AddRange(CallbackDiagnosticPipeline.BuildDiagnostics(
            inputs.SelectMany(static input => input.CallbackDiagnostics),
            cancellationToken));
        result.AddRange(ConstructionDiagnosticPipeline.BuildDiagnostics(
            inputs.SelectMany(static input => input.ConstructionDiagnostics),
            cancellationToken));
        result.AddRange(MemberDiagnosticPipeline.BuildDiagnostics(
            inputs.SelectMany(static input => input.MemberDiagnostics),
            cancellationToken));
        result.AddRange(NestedMappingDiagnosticPipeline.BuildDiagnostics(
            inputs.SelectMany(static input => input.NestedMappingDiagnostics),
            cancellationToken));
        result.AddRange(
            MappingCompletenessDiagnosticPipeline.BuildDiagnostics(
                inputs.SelectMany(static input =>
                    input.MappingCompletenessDiagnostics),
                cancellationToken));
        result.AddRange(IncludeMembersDiagnosticPipeline.BuildDiagnostics(
            inputs.SelectMany(static input =>
                input.IncludeMembersDiagnostics),
            cancellationToken));
        result.AddRange(FlatteningDiagnosticPipeline.BuildDiagnostics(
            inputs.SelectMany(static input =>
                input.FlatteningDiagnostics),
            cancellationToken));

        return result.ToImmutable();
    }

    private static void RegisterSources(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<TypeMapperGenerationInput> models)
    {
        var hintNameAllocations = models
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
        var requests = models
            .Combine(hintNameAllocations)
            .Select(static (source, _) =>
                BuildRequest(source.Left, source.Right))
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildTypeMapperRequests);

        context.RegisterSourceOutput(
            requests,
            static (context, request) =>
                context.AddSource(
                    request.HintName,
                    request.Source));
    }

    private static TypeMapperRequest BuildRequest(
        TypeMapperGenerationInput input,
        HintNameAllocations allocations)
    {
        return new TypeMapperRequest(
            GeneratedSourceHintName.Create(
                "TypeMapper",
                HintNameCollisions.Resolve(
                    allocations,
                    input.StableIdentity)),
            input.Source);
    }
}
