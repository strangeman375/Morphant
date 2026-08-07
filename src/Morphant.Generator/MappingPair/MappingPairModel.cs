using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.MappingPair;

internal readonly record struct MapperMappingRegistrationModel(
    MethodDeclarationSyntax ConfigureSyntax,
    ImmutableArray<MappingPairRegistrationModel> Registrations);

internal readonly record struct MappingPairRegistrationModel(
    InvocationExpressionSyntax Syntax,
    ITypeSymbol SourceType,
    ITypeSymbol DestinationType);

internal readonly record struct MapperMappingPairModel(
    MethodDeclarationSyntax ConfigureSyntax,
    string MapperIdentity,
    ImmutableArray<MappingPairModel> Pairs,
    ImmutableArray<UnsupportedMappingPairModel> UnsupportedPairs,
    bool HasUnifiablePairs);

internal readonly record struct MappingPairModel(
    MappingPairRegistrationModel Registration,
    MappingPairIdentity Identity,
    MappingPairCapabilities Capabilities,
    bool HasUnifiableConflict = false)
{
    public ITypeSymbol SourceType =>
        Registration.SourceType;

    public ITypeSymbol DestinationType =>
        Registration.DestinationType;
}

internal readonly record struct UnsupportedMappingPairModel(
    MappingPairRegistrationModel Registration,
    MappingPairIdentity Identity,
    string Reason,
    bool HasUnifiableConflict = false)
{
    public ITypeSymbol SourceType => Registration.SourceType;

    public ITypeSymbol DestinationType => Registration.DestinationType;
}

internal readonly record struct MappingPairIdentity(
    MappingTypeIdentity Source,
    MappingTypeIdentity Destination);

internal readonly record struct MappingTypeIdentity(
    string Key,
    string DisplayName);

internal readonly record struct MappingPairCapabilities(
    bool Runtime,
    bool Manual,
    MappingConstructionKind Construction,
    bool Members)
{
    public bool StructuredConstruction =>
        Construction == MappingConstructionKind.Structured;

    public bool DirectConstruction =>
        Construction == MappingConstructionKind.Direct;
}

internal enum MappingConstructionKind
{
    Structured,
    Direct
}
