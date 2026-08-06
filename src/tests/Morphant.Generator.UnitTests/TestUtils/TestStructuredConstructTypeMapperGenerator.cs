using Microsoft.CodeAnalysis;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.Settings;
using Morphant.Generator.TypeMapperConfigure;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TestStructuredConstructTypeMapperGenerator
    : IIncrementalGenerator
{
    public void Initialize(
        IncrementalGeneratorInitializationContext context)
    {
        var compilationContext =
            CompilationContextPipeline.Build(context);
        var assemblySettings =
            AssemblyMappingSettingsPipeline.Build(context);
        var configureInfos = TypeMapperConfigurePipeline.Build(
            context,
            compilationContext);
        var pairConfigurations = PairConfigurationPipeline.Build(
            compilationContext,
            configureInfos);
        var mappingPairs = pairConfigurations.SelectMany(
            static (configuration, _) =>
                configuration.SurfaceMappingPairs);

        ConstructionSurfacePipeline.Register(
            context,
            compilationContext,
            mappingPairs);
        TypeMapperPipeline.Register(
            context,
            compilationContext,
            assemblySettings,
            pairConfigurations);
    }
}
