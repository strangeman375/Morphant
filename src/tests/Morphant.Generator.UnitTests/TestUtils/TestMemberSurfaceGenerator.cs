using Microsoft.CodeAnalysis;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MapperBuilderMap;
using Morphant.Generator.MemberSurface;
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
        var mapInfos = MapperBuilderMapPipeline.Build(
            compilationContext,
            configureInfos);
        var mappingPairs = MappingPairPipeline.Build(
            compilationContext,
            mapInfos);

        MemberSurfacePipeline.Register(
            context,
            compilationContext,
            mappingPairs);
    }
}
