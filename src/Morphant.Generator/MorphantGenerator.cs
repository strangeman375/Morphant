using Microsoft.CodeAnalysis;
using Morphant.Generator.MapperBuilderMap;
using Morphant.Generator.Settings;
using Morphant.Generator.TypeMapperGeneration;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator;

[Generator]
public sealed class MorphantGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationContext = CompilationContextPipeline.Build(context);
        var assemblySettings =
            AssemblyMappingSettingsPipeline.Build(context);
        var configureInfos = TypeMapperConfigurePipeline.Build(context, compilationContext);
        var mapInfos = MapperBuilderMapPipeline.Build(
            compilationContext,
            configureInfos);
        TypeMapperPipeline.Register(
            context,
            compilationContext,
            assemblySettings,
            mapInfos);
    }
}
