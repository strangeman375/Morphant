using Microsoft.CodeAnalysis;
using Morphant.Generator.TemplateSurface;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator;

[Generator]
public sealed class MorphantGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationContext = CompilationContextPipeline.Build(context);
        var typeMapperConfigureInfos = TypeMapperConfigurePipeline.Build(context, compilationContext);

        TemplateSurfacePipeline.Register(context, compilationContext, typeMapperConfigureInfos);
    }
}
