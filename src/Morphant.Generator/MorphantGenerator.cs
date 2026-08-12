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
            mapperDeclarations,
            compilationContext);
        var configureInfos = TypeMapperConfigurePipeline.Build(
            configureDeclarations);
        var pairConfigurations = PairConfigurationPipeline.Build(
            compilationContext,
            configureInfos);
        var contractAnalyses = MapperContractPipeline.Build(
            pairConfigurations,
            compilationContext);

        MapperDeclarationDiagnosticPipeline.Register(
            context,
            compilationContext,
            mapperDeclarations,
            contractAnalyses);
        MappingRegistrationDiagnosticPipeline.Register(
            context,
            contractAnalyses);
        ConfigurationFlowDiagnosticPipeline.Register(
            context,
            configureDeclarations,
            contractAnalyses);
        MappingCompositionDiagnosticPipeline.Register(
            context,
            contractAnalyses);
        MappingSettingsDiagnosticPipeline.Register(
            context,
            compilationContext,
            assemblySettings,
            contractAnalyses);
        InheritanceDiagnosticPipeline.Register(
            context,
            contractAnalyses);
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
            compilationContext,
            assemblySettings,
            contractAnalyses);
    }
}
