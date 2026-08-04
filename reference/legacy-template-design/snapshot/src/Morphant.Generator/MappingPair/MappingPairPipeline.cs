using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperBuilderMap;

namespace Morphant.Generator.MappingPair;

internal static class MappingPairPipeline
{
    public static IncrementalValuesProvider<LegacyMapperMappingPairModel> Build(
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValuesProvider<MapperBuilderMapInfo> mapInfos)
    {
        return mapInfos
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                BuildLegacyModel(
                    source.Left,
                    source.Right,
                    cancellationToken))
            .WhereHasValue()
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildMappingPairModels);
    }

    private static LegacyMapperMappingPairModel? BuildLegacyModel(
        MapperBuilderMapInfo mapInfo,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        var registrations = mapInfo.Registrations
            .Select(static registration =>
                new MappingPairRegistrationModel(
                    registration.Syntax,
                    registration.SourceType,
                    registration.DestinationType))
            .ToImmutableArray();
        var mappingPairs = BuildModel(
            new MapperMappingRegistrationModel(
                mapInfo.ConfigureSyntax,
                registrations),
            context,
            cancellationToken);

        if (mappingPairs is not { } model)
        {
            return null;
        }

        var legacyRegistrations =
            ImmutableArray.CreateBuilder<MapperBuilderMapRegistrationInfo>(
                model.Pairs.Length);

        foreach (var pair in model.Pairs)
        {
            var registration = mapInfo.Registrations.First(candidate =>
                candidate.Syntax.SyntaxTree == pair.Registration.Syntax.SyntaxTree &&
                candidate.Syntax.Span == pair.Registration.Syntax.Span);

            legacyRegistrations.Add(registration);
        }

        return new LegacyMapperMappingPairModel(
            model,
            mapInfo.Settings,
            legacyRegistrations.ToImmutable());
    }

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
        var identities = new HashSet<MappingPairIdentityKey>();

        foreach (var registration in mappingInfo.Registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!MappingTypeEligibilityPolicy.IsEligible(
                    registration.SourceType,
                    context.Compilation) ||
                !MappingTypeEligibilityPolicy.IsEligible(
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

            pairs.Add(
                new MappingPairModel(
                    registration,
                    identity,
                    DestinationCapabilityPolicy.Build(
                        registration.DestinationType,
                        context.Compilation,
                        cancellationToken)));
        }

        var immutablePairs = pairs.ToImmutable();

        return new MapperMappingPairModel(
            mappingInfo.ConfigureSyntax,
            SymbolNameHelper.GetFullMetadataName(mapperType),
            immutablePairs,
            HasUnifiablePairs(
                immutablePairs,
                cancellationToken));
    }

    private static bool HasUnifiablePairs(
        ImmutableArray<MappingPairModel> pairs,
        CancellationToken cancellationToken)
    {
        for (var leftIndex = 0;
             leftIndex < pairs.Length;
             leftIndex++)
        {
            for (var rightIndex = leftIndex + 1;
                 rightIndex < pairs.Length;
                 rightIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var left = pairs[leftIndex];
                var right = pairs[rightIndex];

                if (MappingTypeIdentityPolicy.CanPairsUnify(
                        left.SourceType,
                        left.DestinationType,
                        right.SourceType,
                        right.DestinationType))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private readonly record struct MappingPairIdentityKey(
        string Source,
        string Destination);
}
