using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.MapperDeclaration;

internal readonly record struct MapperContractAnalysis(
    MapperPairConfigurationModel Configuration,
    ImmutableArray<MapperContractConflict> Conflicts,
    ImmutableArray<GeneratedMapperContractConflict> GeneratedConflicts)
{
    public bool Excludes(MappingPairIdentity identity)
    {
        return HasDeclaredConflict(identity) ||
               HasGeneratedConflict(identity);
    }

    public bool HasDeclaredConflict(MappingPairIdentity identity)
    {
        return Conflicts.Any(conflict =>
            StringComparer.Ordinal.Equals(
                conflict.PairIdentity.Source.Key,
                identity.Source.Key) &&
            StringComparer.Ordinal.Equals(
                conflict.PairIdentity.Destination.Key,
                identity.Destination.Key));
    }

    public bool HasGeneratedConflict(MappingPairIdentity identity)
    {
        return GeneratedConflicts.Any(conflict =>
            IsIdentity(conflict.EarlierPairIdentity, identity) ||
            IsIdentity(conflict.LaterPairIdentity, identity));
    }

    private static bool IsIdentity(
        MappingPairIdentity left,
        MappingPairIdentity right)
    {
        return StringComparer.Ordinal.Equals(
                   left.Source.Key,
                   right.Source.Key) &&
               StringComparer.Ordinal.Equals(
                   left.Destination.Key,
                   right.Destination.Key);
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

internal readonly record struct GeneratedMapperContractConflict(
    MappingPairRegistrationModel EarlierRegistration,
    MappingPairIdentity EarlierPairIdentity,
    string EarlierContractDisplayName,
    MappingPairRegistrationModel LaterRegistration,
    MappingPairIdentity LaterPairIdentity,
    string LaterContractDisplayName);
