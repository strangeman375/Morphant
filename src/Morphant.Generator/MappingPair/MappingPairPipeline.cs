using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.MappingPair;

internal static class MappingPairPipeline
{
    internal static MapperMappingPairModel? BuildModel(
        MapperMappingRegistrationModel mappingInfo,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var semanticModel = context.Compilation.GetSemanticModel(
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
        var identities = new HashSet<MappingPairIdentityKey>();

        foreach (var registration in mappingInfo.Registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!MappingTypeEligibilityPolicy.CanBeNamed(
                    registration.SourceType,
                    context.Compilation) ||
                !MappingTypeEligibilityPolicy.CanBeNamed(
                    registration.DestinationType,
                    context.Compilation))
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

            if (!identities.Add(identityKey))
            {
                continue;
            }

            var sourceReason =
                MappingTypeEligibilityPolicy.GetUnsupportedRootReason(
                    registration.SourceType,
                    "source",
                    context.Compilation);
            var destinationReason =
                MappingTypeEligibilityPolicy.GetUnsupportedRootReason(
                    registration.DestinationType,
                    "destination",
                    context.Compilation);

            if (sourceReason is not null || destinationReason is not null)
            {
                unsupportedPairs.Add(
                    new UnsupportedMappingPairModel(
                        registration,
                        identity,
                        string.Join(
                            " ",
                            new[] { sourceReason, destinationReason }
                                .Where(static reason => reason is not null))));
                continue;
            }

            pairs.Add(new MappingPairModel(
                registration,
                identity,
                DestinationCapabilityPolicy.Build(
                    registration.SourceType,
                    registration.DestinationType,
                    context.Compilation,
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
        immutableUnsupportedPairs = immutableUnsupportedPairs
            .Select((pair, index) => pair with
            {
                HasUnifiableConflict = unifiable.Unsupported[index]
            })
            .ToImmutableArray();

        return new MapperMappingPairModel(
            mappingInfo.ConfigureSyntax,
            SymbolNameHelper.GetFullMetadataName(mapperType),
            immutablePairs,
            immutableUnsupportedPairs,
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
                pair.SourceType,
                pair.DestinationType))
            .Concat(unsupportedPairs.Select((pair, index) => new Contract(
                Supported: false,
                index,
                pair.SourceType,
                pair.DestinationType)))
            .ToArray();
        var supported = new bool[pairs.Length];
        var unsupported = new bool[unsupportedPairs.Length];
        var hasAny = false;

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
                    hasAny = true;
                }
            }
        }

        return new UnifiableContracts(supported, unsupported, hasAny);

        void SetConflict(Contract contract)
        {
            if (contract.Supported)
            {
                supported[contract.Index] = true;
            }
            else
            {
                unsupported[contract.Index] = true;
            }
        }
    }

    private readonly record struct MappingPairIdentityKey(
        string Source,
        string Destination);

    private readonly record struct Contract(
        bool Supported,
        int Index,
        ITypeSymbol SourceType,
        ITypeSymbol DestinationType);

    private readonly record struct UnifiableContracts(
        bool[] Supported,
        bool[] Unsupported,
        bool HasAny);
}
