using Microsoft.CodeAnalysis;
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
        var compilationContext = CompilationContextPipeline.Build(context);
        context.RegisterSourceOutput(
            compilationContext,
            static (productionContext, compilation) =>
            {
                foreach (var diagnostic in
                         compilation.Compatibility.CreateDiagnostics(
                             compilation.LanguageVersion))
                {
                    productionContext.ReportDiagnostic(diagnostic);
                }
            });
        var assemblySettings =
            AssemblyMappingSettingsPipeline.Build(context);
        var mapperDeclarations = MapperDeclarationPipeline.Build(
            context,
            compilationContext);
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
            compilationContext,
            canonicalSurfacePairs);
        MemberSurfacePipeline.Register(
            context,
            compilationContext,
            canonicalSurfacePairs);

        TypeMapperPipeline.Register(
            context,
            assemblySettings,
            contractAnalyses);
    }
}
