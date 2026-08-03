using Microsoft.CodeAnalysis;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MapperBuilderMap;
using Morphant.Generator.Settings;
using Morphant.Generator.TypeMapperGeneration;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TestTypeMapperGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationContext = CompilationContextPipeline.Build(context);
        var assemblySettings =
            AssemblyMappingSettingsPipeline.Build(context);
        var configureInfos = TypeMapperConfigurePipeline.Build(
            context,
            compilationContext);
        var mapInfos = MapperBuilderMapPipeline.Build(
            compilationContext,
            configureInfos);
        var mappingPairs = MappingPairPipeline.Build(
            compilationContext,
            mapInfos);

        TypeMapperPipeline.Register(
            context,
            compilationContext,
            assemblySettings,
            mappingPairs);
    }
}
