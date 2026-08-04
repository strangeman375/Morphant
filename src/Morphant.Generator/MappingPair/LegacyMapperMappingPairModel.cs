using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperBuilderMap;
using Morphant.Generator.Settings;

namespace Morphant.Generator.MappingPair;

internal readonly record struct LegacyMapperMappingPairModel(
    MapperMappingPairModel MappingPairs,
    MappingSettings Settings,
    ImmutableArray<MapperBuilderMapRegistrationInfo> Registrations)
{
    public MethodDeclarationSyntax ConfigureSyntax =>
        MappingPairs.ConfigureSyntax;

    public string MapperIdentity =>
        MappingPairs.MapperIdentity;

    public ImmutableArray<MappingPairModel> Pairs =>
        MappingPairs.Pairs;

    public bool HasUnifiablePairs =>
        MappingPairs.HasUnifiablePairs;
}
