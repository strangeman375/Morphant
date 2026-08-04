using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.ConstructionSurface.ConstructionPlan;
using Morphant.Generator.ConstructionSurface.PairConfiguration;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.ConstructionSurface;

internal static class ConstructionSurfacePipeline
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
                MorphantGeneratorStageNames
                    .BuildConstructionSurfaceRequests);

        context.RegisterSourceOutput(
            requests,
            static (sourceProductionContext, request) =>
                sourceProductionContext.AddSource(
                    request.HintName,
                    SourceText.From(request.Source, Encoding.UTF8)));
    }

    internal static ImmutableArray<ConstructionSurfaceRequest> BuildRequests(
        ImmutableArray<MapperMappingPairModel> mapperModels,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var pairs = CanonicalMappingPairSelector.Select(
            mapperModels,
            cancellationToken);
        var requests =
            ImmutableArray.CreateBuilder<ConstructionSurfaceRequest>();

        AddConstructionPlanRequests(
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

    private static void AddConstructionPlanRequests(
        ImmutableArray<MappingPairModel> pairs,
        Compilation compilation,
        ImmutableArray<ConstructionSurfaceRequest>.Builder requests,
        CancellationToken cancellationToken)
    {
        var definitions =
            new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);

        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!pair.Capabilities.StructuredConstruction)
            {
                continue;
            }

            var destination =
                DestinationCapabilityPolicy.GetDestinationType(
                    pair.DestinationType,
                    compilation);
            var definition = destination.OriginalDefinition;
            var identity = definition.ContainingAssembly.Identity + "|" +
                           SymbolNameHelper.GetFullMetadataName(definition);

            if (!definitions.ContainsKey(identity))
            {
                definitions.Add(identity, definition);
            }
        }

        var hintNameAllocator = new HintNamePartAllocator();

        foreach (var definition in definitions.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadataName =
                SymbolNameHelper.GetFullMetadataName(definition.Value);
            var planNamespace =
                GeneratedPlanNaming.BuildNamespace(
                    definition.Value);
            var planTypeName =
                GeneratedPlanNaming.BuildConstructionTypeName(
                    definition.Value);
            var model = ConstructionPlanModelBuilder.Build(
                definition.Value,
                planNamespace,
                planTypeName,
                compilation,
                cancellationToken);
            var hintName = GeneratedSourceHintName.Create(
                "Construction",
                hintNameAllocator.Allocate(metadataName));

            requests.Add(
                new ConstructionSurfaceRequest(
                    hintName,
                    ConstructionPlanEmitter.Emit(model)));
        }
    }

    private static void AddPairConfigurationRequests(
        ImmutableArray<MappingPairModel> pairs,
        Compilation compilation,
        ImmutableArray<ConstructionSurfaceRequest>.Builder requests,
        CancellationToken cancellationToken)
    {
        var hintNameAllocator = new HintNamePartAllocator();

        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stableIdentity =
                RemoveGlobalAlias(pair.Identity.Source.DisplayName) +
                "__" +
                RemoveGlobalAlias(pair.Identity.Destination.DisplayName);
            var hintName = GeneratedSourceHintName.Create(
                "MappingExtension",
                hintNameAllocator.Allocate(stableIdentity));
            var model = PairConfigurationModelBuilder.Build(
                pair,
                compilation);

            requests.Add(
                new ConstructionSurfaceRequest(
                    hintName,
                    PairConfigurationEmitter.Emit(model)));
        }
    }

    private static string RemoveGlobalAlias(string value)
    {
        return value.Replace("global::", string.Empty);
    }

    internal readonly record struct ConstructionSurfaceRequest(
        string HintName,
        string Source);
}
