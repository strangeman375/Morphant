using Microsoft.CodeAnalysis;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MemberSurface;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TestMemberSurfaceGenerator : IIncrementalGenerator
{
    public void Initialize(
        IncrementalGeneratorInitializationContext context)
    {
        var compilationContext =
            CompilationContextPipeline.Build(context);
        var configureInfos = TypeMapperConfigurePipeline.Build(
            context,
            compilationContext);
        var pairConfigurations = PairConfigurationPipeline.Build(
            compilationContext,
            configureInfos);
        var mappingPairs = pairConfigurations.SelectMany(
            static (configuration, _) =>
                configuration.SurfaceMappingPairs);

        MemberSurfacePipeline.Register(
            context,
            compilationContext,
            mappingPairs);
    }
}
