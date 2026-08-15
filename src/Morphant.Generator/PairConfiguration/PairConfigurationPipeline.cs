using Microsoft.CodeAnalysis;
using Morphant.Generator.MappingPair;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.PairConfiguration;

internal static class PairConfigurationPipeline
{
    public static IncrementalValuesProvider<MapperPairConfigurationModel>
        Build(
            IncrementalValuesProvider<TypeMapperConfigureInfo> configureInfos)
    {
        var discoveryModels =
            PairConfigurationDiscoveryPipeline.Build(configureInfos);

        return discoveryModels
            .Select(static (discovery, cancellationToken) =>
                TryBuild(discovery, cancellationToken))
            .WhereHasValue()
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildPairConfigurationModels);
    }

    private static MapperPairConfigurationModel? TryBuild(
        PairConfigurationDiscoveryModel discovery,
        CancellationToken cancellationToken)
    {
        var context = discovery.ConfigureInfo.Context;
        var mappingPairs = MappingPairPipeline.BuildModel(
            discovery.MappingRegistrations,
            context,
            cancellationToken);

        return mappingPairs is { } model
            ? PairConfigurationModelBuilder.Build(
                discovery,
                model,
                context,
                cancellationToken)
            : null;
    }
}
