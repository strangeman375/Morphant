using Microsoft.CodeAnalysis;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.MappingPair;
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
        var assemblySettings =
            AssemblyMappingSettingsPipeline.Build(context);
        var configureInfos = TypeMapperConfigurePipeline.Build(context);
        var pairConfigurations = PairConfigurationPipeline.Build(
            context,
            configureInfos);
        var canonicalPairs = CanonicalMappingPairPipeline.Build(
            context,
            pairConfigurations);

        ConstructionSurfacePipeline.Register(
            context,
            canonicalPairs);
        TypeMapperPipeline.Register(
            context,
            assemblySettings,
            pairConfigurations);
    }
}
