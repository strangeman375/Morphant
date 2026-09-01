using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.MappingPair;

internal static class MappingPairPipeline
{
    internal static MapperMappingPairModel? BuildModel(
        MapperMappingRegistrationModel mappingInfo,
        CSharpCompilation compilation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var semanticModel = compilation.GetSemanticModel(
            mappingInfo.ConfigureSyntax.SyntaxTree);

        if (mappingInfo.ConfigureSyntax.Parent is not
                ClassDeclarationSyntax mapperDeclaration ||
            semanticModel.GetDeclaredSymbol(
                mapperDeclaration,
                cancellationToken) is not INamedTypeSymbol mapperType)
        {
            return null;
        }

        var pairs = ImmutableArray.CreateBuilder<MappingPairModel>();
        var unsupportedPairs =
            ImmutableArray.CreateBuilder<UnsupportedMappingPairModel>();
        var unavailablePairs =
            ImmutableArray.CreateBuilder<UnavailableMappingPairModel>();
        var duplicateRegistrations = ImmutableArray.CreateBuilder<
            DuplicateMappingPairRegistrationModel>();
        var authoritativeRegistrations = new Dictionary<
            MappingPairIdentityKey,
            (MappingPairRegistrationModel Registration,
                MappingPairIdentity Identity)>();

        foreach (var registration in mappingInfo.Registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceNameability =
                MappingTypeEligibilityPolicy.GetNameability(
                    registration.SourceType,
                    compilation);
            var destinationNameability =
                MappingTypeEligibilityPolicy.GetNameability(
                    registration.DestinationType,
                    compilation);

            if (sourceNameability == MappingTypeNameability.CompilerOwned ||
                destinationNameability ==
                    MappingTypeNameability.CompilerOwned)
            {
                continue;
            }

            var identity = new MappingPairIdentity(
                MappingTypeIdentityPolicy.Create(
                    registration.SourceType),
                MappingTypeIdentityPolicy.Create(
                    registration.DestinationType));
            var identityKey = new MappingPairIdentityKey(
                identity.Source.Key,
                identity.Destination.Key);

            if (authoritativeRegistrations.TryGetValue(
                    identityKey,
                    out var authoritative))
            {
                duplicateRegistrations.Add(
                    new DuplicateMappingPairRegistrationModel(
                        registration,
                        authoritative.Registration,
                        authoritative.Identity));
                continue;
            }

            authoritativeRegistrations.Add(
                identityKey,
                (registration, identity));

            var unavailableTypes =
                ImmutableArray.CreateBuilder<UnavailableMappingTypeModel>(2);

            if (sourceNameability == MappingTypeNameability.Unavailable)
            {
                unavailableTypes.Add(new UnavailableMappingTypeModel(
                    MappingTypeRole.Source,
                    registration.SourceType));
            }

            if (destinationNameability == MappingTypeNameability.Unavailable)
            {
                unavailableTypes.Add(new UnavailableMappingTypeModel(
                    MappingTypeRole.Destination,
                    registration.DestinationType));
            }

            if (unavailableTypes.Count != 0)
            {
                unavailablePairs.Add(new UnavailableMappingPairModel(
                    registration,
                    identity,
                    unavailableTypes.ToImmutable()));
                continue;
            }

            var sourceReason =
                MappingTypeEligibilityPolicy.GetUnsupportedRootReason(
                    registration.SourceType);
            var destinationReason =
                MappingTypeEligibilityPolicy.GetUnsupportedRootReason(
                    registration.DestinationType);

            if (sourceReason is not null || destinationReason is not null)
            {
                var unsupportedRoots = ImmutableArray.CreateBuilder<
                    UnsupportedMappingRootModel>(2);

                if (sourceReason is not null)
                {
                    unsupportedRoots.Add(new UnsupportedMappingRootModel(
                        MappingTypeRole.Source,
                        registration.SourceType,
                        sourceReason));
                }

                if (destinationReason is not null)
                {
                    unsupportedRoots.Add(new UnsupportedMappingRootModel(
                        MappingTypeRole.Destination,
                        registration.DestinationType,
                        destinationReason));
                }

                unsupportedPairs.Add(
                    new UnsupportedMappingPairModel(
                        registration,
                        identity,
                        unsupportedRoots.ToImmutable()));
                continue;
            }

            pairs.Add(new MappingPairModel(
                registration,
                identity,
                DestinationCapabilityPolicy.Build(
                    registration.SourceType,
                    registration.DestinationType,
                    compilation,
                    cancellationToken)));
        }

        var immutablePairs = pairs.ToImmutable();
        var immutableUnsupportedPairs = unsupportedPairs.ToImmutable();
        var unifiable = FindUnifiableContracts(
            immutablePairs,
            immutableUnsupportedPairs,
            cancellationToken);

        immutablePairs = immutablePairs
            .Select((pair, index) => pair with
            {
                HasUnifiableConflict = unifiable.Supported[index]
            })
            .ToImmutableArray();

        return new MapperMappingPairModel(
            mappingInfo.ConfigureSyntax,
            SymbolNameHelper.GetFullMetadataName(mapperType),
            immutablePairs,
            immutableUnsupportedPairs,
            unavailablePairs.ToImmutable(),
            duplicateRegistrations.ToImmutable(),
            unifiable.Conflicts,
            unifiable.HasAny);
    }

    private static UnifiableContracts FindUnifiableContracts(
        ImmutableArray<MappingPairModel> pairs,
        ImmutableArray<UnsupportedMappingPairModel> unsupportedPairs,
        CancellationToken cancellationToken)
    {
        var contracts = pairs
            .Select((pair, index) => new Contract(
                Supported: true,
                index,
                pair.Registration,
                pair.Identity,
                pair.SourceType,
                pair.DestinationType))
            .Concat(unsupportedPairs.Select((pair, index) => new Contract(
                Supported: false,
                index,
                pair.Registration,
                pair.Identity,
                pair.SourceType,
                pair.DestinationType)))
            .OrderBy(static contract =>
                contract.Registration.Syntax.SpanStart)
            .ThenBy(static contract => contract.Identity.Source.Key,
                StringComparer.Ordinal)
            .ThenBy(static contract => contract.Identity.Destination.Key,
                StringComparer.Ordinal)
            .ToArray();
        var supported = new bool[pairs.Length];
        var conflicts = ImmutableArray.CreateBuilder<
            UnifiableMappingPairConflictModel>();

        for (var leftIndex = 0;
             leftIndex < contracts.Length;
             leftIndex++)
        {
            for (var rightIndex = leftIndex + 1;
                 rightIndex < contracts.Length;
                 rightIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var left = contracts[leftIndex];
                var right = contracts[rightIndex];

                if (MappingTypeIdentityPolicy.CanPairsUnify(
                        left.SourceType,
                        left.DestinationType,
                        right.SourceType,
                        right.DestinationType))
                {
                    SetConflict(left);
                    SetConflict(right);
                    conflicts.Add(new UnifiableMappingPairConflictModel(
                        left.Registration,
                        left.Identity,
                        right.Registration,
                        right.Identity));
                }
            }
        }

        return new UnifiableContracts(
            supported,
            conflicts.ToImmutable());

        void SetConflict(Contract contract)
        {
            if (contract.Supported)
            {
                supported[contract.Index] = true;
            }
        }
    }

    private readonly record struct MappingPairIdentityKey(
        string Source,
        string Destination);

    private readonly record struct Contract(
        bool Supported,
        int Index,
        MappingPairRegistrationModel Registration,
        MappingPairIdentity Identity,
        ITypeSymbol SourceType,
        ITypeSymbol DestinationType);

    private readonly record struct UnifiableContracts(
        bool[] Supported,
        ImmutableArray<UnifiableMappingPairConflictModel> Conflicts)
    {
        public bool HasAny => !Conflicts.IsEmpty;
    }
}
