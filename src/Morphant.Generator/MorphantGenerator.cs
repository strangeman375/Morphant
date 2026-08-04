using Microsoft.CodeAnalysis;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.MemberSurface;
using Morphant.Generator.PairConfiguration;
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
        var pairConfigurations = PairConfigurationPipeline.Build(
            compilationContext,
            configureInfos);
        var surfaceMappingPairs = pairConfigurations.Select(
            static (configuration, _) => configuration.MappingPairs);
        ConstructionSurfacePipeline.Register(
            context,
            compilationContext,
            surfaceMappingPairs);
        MemberSurfacePipeline.Register(
            context,
            compilationContext,
            surfaceMappingPairs);

        TypeMapperPipeline.Register(
            context,
            compilationContext,
            assemblySettings,
            pairConfigurations);
    }
}
