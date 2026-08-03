using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperBuilderMap;
using Morphant.Generator.Settings;

namespace Morphant.Generator.MappingPair;

internal readonly record struct MapperMappingPairModel(
    MethodDeclarationSyntax ConfigureSyntax,
    MappingSettings Settings,
    string MapperIdentity,
    ImmutableArray<MappingPairModel> Pairs,
    bool HasUnifiablePairs);

internal readonly record struct MappingPairModel(
    MapperBuilderMapRegistrationInfo Registration,
    MappingPairIdentity Identity,
    MappingPairCapabilities Capabilities)
{
    public ITypeSymbol SourceType =>
        Registration.SourceType;

    public ITypeSymbol DestinationType =>
        Registration.DestinationType;
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
