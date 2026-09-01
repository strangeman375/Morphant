using Microsoft.CodeAnalysis;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TestConstructionSurfaceGenerator :
    IIncrementalGenerator
{
    public void Initialize(
        IncrementalGeneratorInitializationContext context)
    {
        var configureInfos = TypeMapperConfigurePipeline.Build(context);
        var pairConfigurations = PairConfigurationPipeline.Build(
            context,
            configureInfos);
        var canonicalPairs = CanonicalMappingPairPipeline.Build(
            context,
            pairConfigurations);

        ConstructionSurfacePipeline.Register(
            context,
            canonicalPairs);
    }
}
