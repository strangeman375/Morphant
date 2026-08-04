using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.ConstructionSurface.PairConfiguration;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MemberSurface.MemberPlan;
using Morphant.Generator.MemberSurface.PairConfiguration;

namespace Morphant.Generator.MemberSurface;

internal static class MemberSurfacePipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValuesProvider<MapperMappingPairModel> mappingPairModels)
    {
        var requests = mappingPairModels
            .Collect()
            .Combine(compilationContext)
            .SelectMany(static (source, cancellationToken) =>
                BuildRequests(
                    source.Left,
                    source.Right.Compilation,
                    cancellationToken))
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildMemberSurfaceRequests);

        context.RegisterSourceOutput(
            requests,
            static (sourceProductionContext, request) =>
                sourceProductionContext.AddSource(
                    request.HintName,
                    SourceText.From(request.Source, Encoding.UTF8)));
    }

    internal static ImmutableArray<MemberSurfaceRequest> BuildRequests(
        ImmutableArray<MapperMappingPairModel> mapperModels,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var pairs = CanonicalMappingPairSelector.Select(
            mapperModels,
            cancellationToken);
        var requests = ImmutableArray.CreateBuilder<MemberSurfaceRequest>();

        AddMemberPlanRequests(
            pairs,
            compilation,
            requests,
            cancellationToken);
        AddPairConfigurationRequests(
            pairs,
            compilation,
            requests,
            cancellationToken);

        return requests.ToImmutable();
    }

    private static void AddMemberPlanRequests(
        ImmutableArray<MappingPairModel> pairs,
        Compilation compilation,
        ImmutableArray<MemberSurfaceRequest>.Builder requests,
        CancellationToken cancellationToken)
    {
        var definitions =
            new Dictionary<string, MemberPlanDefinition>(
                StringComparer.Ordinal);

        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!pair.Capabilities.Members)
            {
                continue;
            }

            var destination = DestinationCapabilityPolicy
                .GetDestinationType(
                    pair.DestinationType,
                    compilation);
            var definition = destination.OriginalDefinition;
            var identity = definition.ContainingAssembly.Identity + "|" +
                           SymbolNameHelper.GetFullMetadataName(definition);

            if (!definitions.ContainsKey(identity))
            {
                definitions.Add(
                    identity,
                    new MemberPlanDefinition(
                        definition,
                        pair.Capabilities.StructuredConstruction));
            }
        }

        var hintNameAllocator = new HintNamePartAllocator();

        foreach (var definition in definitions.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadataName = SymbolNameHelper.GetFullMetadataName(
                definition.Value.DestinationType);
            var model = MemberPlanModelBuilder.Build(
                definition.Value.DestinationType,
                definition.Value.IncludeInitOnlyProperties,
                compilation,
                cancellationToken);
            var hintName = GeneratedSourceHintName.Create(
                "Member",
                hintNameAllocator.Allocate(metadataName));

            requests.Add(
                new MemberSurfaceRequest(
                    hintName,
                    MemberPlanEmitter.Emit(model)));
        }
    }

    private static void AddPairConfigurationRequests(
        ImmutableArray<MappingPairModel> pairs,
        Compilation compilation,
        ImmutableArray<MemberSurfaceRequest>.Builder requests,
        CancellationToken cancellationToken)
    {
        var hintNameAllocator = new HintNamePartAllocator();

        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!pair.Capabilities.Members)
            {
                continue;
            }

            var stableIdentity =
                RemoveGlobalAlias(pair.Identity.Source.DisplayName) +
                "__" +
                RemoveGlobalAlias(pair.Identity.Destination.DisplayName);
            var hintName = GeneratedSourceHintName.Create(
                "MemberExtension",
                hintNameAllocator.Allocate(stableIdentity));
            var model = PairConfigurationModelBuilder.Build(
                pair,
                compilation);

            requests.Add(
                new MemberSurfaceRequest(
                    hintName,
                    MemberConfigurationEmitter.Emit(model)));
        }
    }

    private static string RemoveGlobalAlias(string value)
    {
        return value.Replace("global::", string.Empty);
    }

    private readonly record struct MemberPlanDefinition(
        INamedTypeSymbol DestinationType,
        bool IncludeInitOnlyProperties);

    internal readonly record struct MemberSurfaceRequest(
        string HintName,
        string Source);
}
