using Microsoft.CodeAnalysis;
using Morphant.Generator.MappingPair;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.PairConfiguration;

internal static class PairConfigurationPipeline
{
    public static IncrementalValuesProvider<MapperPairConfigurationModel>
        Build(
            IncrementalGeneratorInitializationContext context,
            IncrementalValuesProvider<TypeMapperConfigureInfo> configureInfos)
    {
        var discoveryModels =
            PairConfigurationDiscoveryPipeline.Build(
                context,
                configureInfos);

        return GeneratorStageGuard
            .Select(
                context,
                discoveryModels,
                MorphantGeneratorStageNames.BuildPairConfigurationModels,
                static (discovery, cancellationToken) =>
                    TryBuild(discovery, cancellationToken),
                static discovery => discovery.ConfigureInfo.Syntax
                    .Identifier.GetLocation())
            .WhereHasValue()
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildPairConfigurationModels);
    }

    private static MapperPairConfigurationModel? TryBuild(
        PairConfigurationDiscoveryModel discovery,
        CancellationToken cancellationToken)
    {
        var compilation = discovery.ConfigureInfo.Declaration?.Compilation ??
            throw new InvalidOperationException(
                "The root mapper configuration must have a declaration model.");
        var mappingPairs = MappingPairPipeline.BuildModel(
            discovery.MappingRegistrations,
            compilation,
            cancellationToken);

        return mappingPairs is { } model
            ? PairConfigurationModelBuilder.Build(
                discovery,
                model,
                compilation,
                cancellationToken)
            : null;
    }
}
