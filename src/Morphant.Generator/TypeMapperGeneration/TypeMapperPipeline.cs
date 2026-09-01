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
            MapperContractPipeline.Build(context, mapperConfigurations));
    }

    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<MappingSettings> assemblySettings,
        IncrementalValuesProvider<MapperContractAnalysis> contractAnalyses)
    {
        var models = GeneratorStageGuard
            .Select(
                context,
                contractAnalyses.Combine(assemblySettings),
                MorphantGeneratorStageNames.BuildTypeMapperModels,
                static (source, cancellationToken) =>
                    TypeMapperModelBuilder.TryBuild(
                        (source.Left, source.Right),
                        cancellationToken),
                static source => source.Left.Configuration.Declaration
                    .AttributedDeclaration.Identifier.GetLocation())
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
        var diagnostics = GeneratorStageGuard.Select(
            context,
            models.Collect(),
            "BuildTypeMapperDiagnostics",
            static (inputs, cancellationToken) =>
                BuildDiagnostics(inputs, cancellationToken),
            ImmutableArray<Diagnostic>.Empty);

        DiagnosticPipeline.Register(
            context,
            diagnostics,
            "TypeMapperDiagnostics");
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
        var hintNameIdentities = models
            .Select(static (model, _) =>
                new HintNameIdentity(
                    model.StableIdentity,
                    HintNameHelper.ToHintNamePart(
                        model.StableIdentity)));
        var hintNameAllocations = GeneratorStageGuard.Select(
                context,
                hintNameIdentities.Collect(),
                "AllocateTypeMapperHintNames",
                static (identities, cancellationToken) =>
                    HintNameCollisions.Build(
                        identities,
                        cancellationToken),
                new HintNameAllocations(
                    ImmutableArray<HintNameAllocation>.Empty))
            .WithComparer(HintNameAllocationsComparer.Instance);
        var requests = GeneratorStageGuard.SelectTrackedSourceRequest(
                context,
                models.Combine(hintNameAllocations),
                MorphantGeneratorStageNames.BuildTypeMapperRequests,
                static (source, _) =>
                    BuildRequest(source.Left, source.Right),
                static _ => Location.None);

        GeneratorStageGuard.RegisterSourceOutput(
            context,
            requests,
            "AddTypeMapperSource",
            static request => request.HintName,
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
