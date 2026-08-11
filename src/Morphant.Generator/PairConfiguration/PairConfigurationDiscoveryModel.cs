using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.PairConfiguration;

internal readonly record struct PairConfigurationDiscoveryModel(
    TypeMapperConfigureInfo ConfigureInfo,
    MapperMappingRegistrationModel MappingRegistrations,
    ImmutableArray<PairConfigurationDiscoveryLevel> Levels,
    ImmutableArray<UnavailableBaseConfigurationModel>
        UnavailableBaseConfigurations,
    ImmutableArray<BuilderFlowBreakModel> FlowBreaks,
    bool HasInvalidBaseConfiguration)
{
    public bool HasUnavailableBaseConfiguration =>
        !UnavailableBaseConfigurations.IsEmpty;
}

internal readonly record struct PairConfigurationDiscoveryLevel(
    TypeMapperConfigureInfo ConfigureInfo,
    INamedTypeSymbol ConstructedMapperType,
    MapperMappingRegistrationModel BindingRegistrations,
    MapperMappingRegistrationModel InstantiatedRegistrations,
    ImmutableArray<PairConfigurationInvocationChain> InvocationChains,
    ImmutableArray<InvocationExpressionSyntax> BaseConfigureCalls,
    ImmutableArray<BuilderFlowBreakModel> FlowBreaks);

internal readonly record struct PairConfigurationInvocationChain(
    ImmutableArray<InvocationExpressionSyntax> Invocations);

internal sealed record UnavailableBaseConfigurationModel(
    InvocationExpressionSyntax Invocation,
    INamedTypeSymbol BaseMapperType,
    int LevelOrder);

internal sealed record BuilderFlowBreakModel(
    BuilderFlowBreakKind Kind,
    Location Location,
    MappingPairRegistrationModel? Registration,
    int LevelOrder);

internal enum BuilderFlowBreakKind
{
    Mapper,
    Mapping
}
