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
    bool HasUnavailableBaseConfiguration);

internal readonly record struct PairConfigurationDiscoveryLevel(
    TypeMapperConfigureInfo ConfigureInfo,
    INamedTypeSymbol ConstructedMapperType,
    MapperMappingRegistrationModel BindingRegistrations,
    MapperMappingRegistrationModel InstantiatedRegistrations,
    ImmutableArray<PairConfigurationInvocationChain> InvocationChains,
    ImmutableArray<InvocationExpressionSyntax> BaseConfigureCalls);

internal readonly record struct PairConfigurationInvocationChain(
    ImmutableArray<InvocationExpressionSyntax> Invocations);
