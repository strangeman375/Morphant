using Microsoft.CodeAnalysis;
using Morphant.Generator.TemplateSurface;
using Morphant.Generator.TemplateSurface.TemplateExtension;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TestTemplateExtensionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationContext = CompilationContextPipeline.Build(context);
        var configureInfos = TypeMapperConfigurePipeline.Build(
            context,
            compilationContext);

        var destinationTypes = TemplateDestinationTypePipeline.Build(
            compilationContext,
            configureInfos);

        TemplateExtensionPipeline.Register(context, destinationTypes);
    }
}
