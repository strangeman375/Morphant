using Microsoft.CodeAnalysis;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.MemberSurface;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.Settings;
using Morphant.Generator.TypeMapperConfigure;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TestBasicMembersTypeMapperGenerator
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
        var mappingPairs = pairConfigurations.Select(
            static (configuration, _) =>
                configuration.MappingPairs);

        ConstructionSurfacePipeline.Register(
            context,
            compilationContext,
            mappingPairs);
        MemberSurfacePipeline.Register(
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
