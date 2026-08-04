using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.PairConfiguration;

internal readonly record struct PairConfigurationDiscoveryModel(
    TypeMapperConfigureInfo ConfigureInfo,
    MapperMappingRegistrationModel MappingRegistrations,
    ImmutableArray<PairConfigurationInvocationChain> InvocationChains);

internal readonly record struct PairConfigurationInvocationChain(
    ImmutableArray<InvocationExpressionSyntax> Invocations);
