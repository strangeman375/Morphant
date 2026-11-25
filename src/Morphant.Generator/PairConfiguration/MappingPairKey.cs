using Microsoft.CodeAnalysis;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.PairConfiguration;

internal readonly record struct MappingPairKey(string Source, string Destination)
{
    public static MappingPairKey Create(MappingPairModel pair) =>
        Create(pair.SourceType, pair.DestinationType);

    public static MappingPairKey Create(ITypeSymbol source, ITypeSymbol destination) =>
        new(MappingTypeIdentityPolicy.Create(source).Key, MappingTypeIdentityPolicy.Create(destination).Key);
}
