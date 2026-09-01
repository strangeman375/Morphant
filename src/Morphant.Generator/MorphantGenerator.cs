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
        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(
                context.ParseOptionsProvider),
            static (productionContext, source) =>
            {
                var compilation = (CSharpCompilation)source.Left;
                var languageVersion =
                    ((CSharpParseOptions)source.Right).LanguageVersion;
                var compatibility =
                    CompilationCompatibilityDetector.Detect(
                        compilation,
                        languageVersion);

                foreach (var diagnostic in
                         compatibility.CreateDiagnostics(languageVersion))
                {
                    productionContext.ReportDiagnostic(diagnostic);
                }
            });
        var assemblySettings =
            AssemblyMappingSettingsPipeline.Build(context);
        var mapperDeclarations = MapperDeclarationPipeline.Build(context);
        var configureDeclarations =
            TypeMapperConfigurePipeline.BuildDeclarations(
                mapperDeclarations);
        var configureInfos = TypeMapperConfigurePipeline.Build(
            configureDeclarations);
        var pairConfigurations =
            PairConfigurationPipeline.Build(configureInfos);
        var contractAnalyses =
            MapperContractPipeline.Build(pairConfigurations);
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
