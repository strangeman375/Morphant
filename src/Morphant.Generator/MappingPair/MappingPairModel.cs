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
    ImmutableArray<UnavailableMappingPairModel> UnavailablePairs,
    ImmutableArray<DuplicateMappingPairRegistrationModel>
        DuplicateRegistrations,
    ImmutableArray<UnifiableMappingPairConflictModel> UnifiableConflicts,
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
    ImmutableArray<UnsupportedMappingRootModel> UnsupportedRoots)
{
    public ITypeSymbol SourceType => Registration.SourceType;

    public ITypeSymbol DestinationType => Registration.DestinationType;
}

internal readonly record struct UnavailableMappingPairModel(
    MappingPairRegistrationModel Registration,
    MappingPairIdentity Identity,
    ImmutableArray<UnavailableMappingTypeModel> UnavailableTypes);

internal readonly record struct UnavailableMappingTypeModel(
    MappingTypeRole Role,
    ITypeSymbol Type);

internal readonly record struct UnsupportedMappingRootModel(
    MappingTypeRole Role,
    ITypeSymbol Type,
    string Reason);

internal readonly record struct DuplicateMappingPairRegistrationModel(
    MappingPairRegistrationModel Registration,
    MappingPairRegistrationModel AuthoritativeRegistration,
    MappingPairIdentity Identity);

internal readonly record struct UnifiableMappingPairConflictModel(
    MappingPairRegistrationModel EarlierRegistration,
    MappingPairIdentity EarlierIdentity,
    MappingPairRegistrationModel LaterRegistration,
    MappingPairIdentity LaterIdentity);

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

internal enum MappingTypeRole
{
    Source,
    Destination
}

internal enum MappingConstructionKind
{
    Structured,
    Direct
}
