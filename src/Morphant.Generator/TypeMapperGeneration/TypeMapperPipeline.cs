using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        var fileScopedNamespace = context.ParseOptionsProvider.Select(
            static (options, _) => ((CSharpParseOptions)options).LanguageVersion >= LanguageVersion.CSharp10);
        var models = GeneratorStageGuard
            .Select(
                context,
                contractAnalyses.Combine(assemblySettings).Combine(fileScopedNamespace),
                MorphantGeneratorStageNames.BuildTypeMapperModels,
                static (source, cancellationToken) =>
                    TypeMapperModelBuilder.TryBuild(
                        source.Left,
                        cancellationToken,
                        source.Right),
                static source => source.Left.Left.Configuration.Declaration
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
        var requests = GeneratorStageGuard.SelectTrackedSourceRequest(
                context,
                models,
                MorphantGeneratorStageNames.BuildTypeMapperRequests,
                static (source, _) =>
                    new TypeMapperRequest(source.HintName, source.Source),
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

}
