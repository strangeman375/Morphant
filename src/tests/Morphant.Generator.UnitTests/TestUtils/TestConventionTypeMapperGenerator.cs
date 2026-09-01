using Microsoft.CodeAnalysis;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.Settings;
using Morphant.Generator.TypeMapperConfigure;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TestConventionTypeMapperGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var assemblySettings =
            AssemblyMappingSettingsPipeline.Build(context);
        var configureInfos = TypeMapperConfigurePipeline.Build(context);
        var pairConfigurations = PairConfigurationPipeline.Build(
            configureInfos);

        TypeMapperPipeline.Register(
            context,
            assemblySettings,
            pairConfigurations);
    }
}
