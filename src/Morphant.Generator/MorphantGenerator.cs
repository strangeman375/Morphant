using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.Compatibility;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.MemberSurface;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.Settings;
using Morphant.Generator.TypeMapperGeneration;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator;

[Generator]
internal sealed class MorphantGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        try
        {
            InitializeCore(context);
        }
        catch (Exception exception) when (GeneratorStageGuard.CanReport(
                   exception,
                   CancellationToken.None))
        {
            GeneratorStageGuard.RegisterInitializationFailure(
                context,
                "Initialize",
                exception);
        }
    }

    private static void InitializeCore(
        IncrementalGeneratorInitializationContext context)
    {
        var compatibilityDiagnostics = GeneratorStageGuard.Select(
            context,
            context.CompilationProvider.Combine(
                context.ParseOptionsProvider),
            "DetectCompilationCompatibility",
            static (source, _) =>
            {
                var compilation = (CSharpCompilation)source.Left;
                var languageVersion =
                    ((CSharpParseOptions)source.Right).LanguageVersion;
                var compatibility =
                    CompilationCompatibilityDetector.Detect(
                        compilation,
                        languageVersion);

                return compatibility.CreateDiagnostics(languageVersion);
            },
            ImmutableArray<Diagnostic>.Empty);
        DiagnosticPipeline.Register(
            context,
            compatibilityDiagnostics,
            "CompatibilityDiagnostics");
        var assemblySettings =
            AssemblyMappingSettingsPipeline.Build(context);
        var mapperDeclarations = MapperDeclarationPipeline.Build(context);
        var configureDeclarations =
            TypeMapperConfigurePipeline.BuildDeclarations(
                context,
                mapperDeclarations);
        var configureInfos = TypeMapperConfigurePipeline.Build(
            configureDeclarations);
        var pairConfigurations =
            PairConfigurationPipeline.Build(context, configureInfos);
        var contractAnalyses =
            MapperContractPipeline.Build(context, pairConfigurations);
        var contractAnalysisCollection = contractAnalyses.Collect();

        MapperDeclarationDiagnosticPipeline.Register(
            context,
            mapperDeclarations,
            contractAnalysisCollection);
        MappingRegistrationDiagnosticPipeline.Register(
            context,
            contractAnalysisCollection);
        ConfigurationFlowDiagnosticPipeline.Register(
            context,
            configureDeclarations,
            contractAnalysisCollection);
        MappingCompositionDiagnosticPipeline.Register(
            context,
            contractAnalysisCollection);
        MappingSettingsDiagnosticPipeline.Register(
            context,
            assemblySettings,
            contractAnalysisCollection);
        PolymorphismDiagnosticPipeline.Register(
            context,
            contractAnalysisCollection);
        InheritanceDiagnosticPipeline.Register(
            context,
            contractAnalysisCollection);
        var canonicalSurfacePairs = CanonicalMappingPairPipeline.Build(
            context,
            pairConfigurations);
        ConstructionSurfacePipeline.Register(
            context,
            canonicalSurfacePairs);
        MemberSurfacePipeline.Register(
            context,
            canonicalSurfacePairs);

        TypeMapperPipeline.Register(
            context,
            assemblySettings,
            contractAnalyses);
    }
}
