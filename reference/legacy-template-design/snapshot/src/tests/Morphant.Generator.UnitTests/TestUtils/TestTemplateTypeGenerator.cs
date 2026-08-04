using Microsoft.CodeAnalysis;
using Morphant.Generator.MapperBuilderMap;
using Morphant.Generator.Settings;
using Morphant.Generator.TemplateSurface;
using Morphant.Generator.TemplateSurface.TemplateType;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TestTemplateTypeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationContext = CompilationContextPipeline.Build(context);
        var assemblySettings =
            AssemblyMappingSettingsPipeline.Build(context);
        var typeMapperConfigureInfos = TypeMapperConfigurePipeline.Build(context, compilationContext);
        var mapInfos = MapperBuilderMapPipeline.Build(
            compilationContext,
            typeMapperConfigureInfos);
        var destinationTypeInfos = TemplateDestinationTypePipeline.Build(
            compilationContext,
            assemblySettings,
            mapInfos);

        TemplateTypePipeline.Register(context, compilationContext, destinationTypeInfos);
    }
}
