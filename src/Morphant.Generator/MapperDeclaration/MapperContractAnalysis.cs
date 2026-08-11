using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.MapperDeclaration;

internal readonly record struct MapperContractAnalysis(
    MapperPairConfigurationModel Configuration,
    ImmutableArray<MapperContractConflict> Conflicts)
{
    public bool Excludes(MappingPairIdentity identity)
    {
        return Conflicts.Any(conflict =>
            StringComparer.Ordinal.Equals(
                conflict.PairIdentity.Source.Key,
                identity.Source.Key) &&
            StringComparer.Ordinal.Equals(
                conflict.PairIdentity.Destination.Key,
                identity.Destination.Key));
    }
}

internal readonly record struct MapperContractConflict(
    MapperContractConflictKind Kind,
    MappingPairRegistrationModel Registration,
    MappingPairIdentity PairIdentity,
    string ContractDisplayName,
    ImmutableArray<TypeSyntax> InterfaceSyntaxes);

internal enum MapperContractConflictKind
{
    Exact,
    Unifiable
}
