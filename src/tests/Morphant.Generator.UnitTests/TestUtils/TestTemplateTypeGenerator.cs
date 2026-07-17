using Microsoft.CodeAnalysis;
using Morphant.Generator.TemplateSurface;
using Morphant.Generator.TemplateSurface.TemplateType;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TestTemplateTypeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationContext = CompilationContextPipeline.Build(context);
        var typeMapperConfigureInfos = TypeMapperConfigurePipeline.Build(context, compilationContext);
        var destinationTypeInfos = TemplateDestinationTypePipeline.Build(compilationContext, typeMapperConfigureInfos);

        TemplateTypePipeline.Register(context, compilationContext, destinationTypeInfos);
    }
}
