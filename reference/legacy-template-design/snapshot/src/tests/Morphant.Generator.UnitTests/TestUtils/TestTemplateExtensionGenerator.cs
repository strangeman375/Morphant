using Microsoft.CodeAnalysis;
using Morphant.Generator.MapperBuilderMap;
using Morphant.Generator.Settings;
using Morphant.Generator.TemplateSurface;
using Morphant.Generator.TemplateSurface.TemplateExtension;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TestTemplateExtensionGenerator : IIncrementalGenerator
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

        var destinationTypes = TemplateDestinationTypePipeline.Build(
            compilationContext,
            assemblySettings,
            mapInfos);

        TemplateExtensionPipeline.Register(context, destinationTypes);
    }
}
