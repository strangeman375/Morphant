using Microsoft.CodeAnalysis;
using Morphant.Generator.MappingPair;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.PairConfiguration;

internal static class PairConfigurationPipeline
{
    public static IncrementalValuesProvider<MapperPairConfigurationModel>
        Build(
            IncrementalValueProvider<CompilationContext> compilationContext,
            IncrementalValuesProvider<TypeMapperConfigureInfo> configureInfos)
    {
        var discoveryModels = PairConfigurationDiscoveryPipeline.Build(
            compilationContext,
            configureInfos);

        return discoveryModels
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                TryBuild(source, cancellationToken))
            .WhereHasValue()
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildPairConfigurationModels);
    }

    private static MapperPairConfigurationModel? TryBuild(
        (
            PairConfigurationDiscoveryModel Discovery,
            CompilationContext Context
        ) source,
        CancellationToken cancellationToken)
    {
        var mappingPairs = MappingPairPipeline.BuildModel(
            source.Discovery.MappingRegistrations,
            source.Context,
            cancellationToken);

        return mappingPairs is { } model
            ? PairConfigurationModelBuilder.Build(
                source.Discovery,
                model,
                source.Context,
                cancellationToken)
            : null;
    }
}
