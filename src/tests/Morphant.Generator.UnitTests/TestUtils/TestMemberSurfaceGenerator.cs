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
        var configureInfos = TypeMapperConfigurePipeline.Build(context);
        var pairConfigurations = PairConfigurationPipeline.Build(
            context,
            configureInfos);
        var canonicalPairs = CanonicalMappingPairPipeline.Build(
            context,
            pairConfigurations);

        MemberSurfacePipeline.Register(
            context,
            canonicalPairs);
    }
}
