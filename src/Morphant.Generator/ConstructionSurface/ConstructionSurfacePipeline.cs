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

    private static ImmutableArray<ConstructionSurfaceRequest> BuildRequests(
        ImmutableArray<MapperMappingPairModel> mapperModels,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var pairs = SelectCanonicalPairs(
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

    private static ImmutableArray<MappingPairModel> SelectCanonicalPairs(
        ImmutableArray<MapperMappingPairModel> mapperModels,
        CancellationToken cancellationToken)
    {
        var candidates =
            new Dictionary<string, MappingPairModel>(StringComparer.Ordinal);

        foreach (var mapperModel in mapperModels)
        {
            foreach (var pair in mapperModel.Pairs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var key = BuildGeneratedSignatureIdentity(pair);

                if (!candidates.TryGetValue(key, out var current) ||
                    CompareRepresentation(pair, current) < 0)
                {
                    candidates[key] = pair;
                }
            }
        }

        return candidates
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => pair.Value)
            .ToImmutableArray();
    }

    private static string BuildGeneratedSignatureIdentity(
        MappingPairModel pair)
    {
        return MappingTypeIdentityPolicy.CreateAlphaEquivalentPairKey(
            pair.SourceType,
            pair.DestinationType);
    }

    private static int CompareRepresentation(
        MappingPairModel left,
        MappingPairModel right)
    {
        var comparison = CompareTypeRepresentation(
            left.SourceType,
            right.SourceType);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = CompareTypeRepresentation(
            left.DestinationType,
            right.DestinationType);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = StringComparer.Ordinal.Compare(
            left.Identity.Source.Key,
            right.Identity.Source.Key);

        return comparison != 0
            ? comparison
            : StringComparer.Ordinal.Compare(
                left.Identity.Destination.Key,
                right.Identity.Destination.Key);
    }

    private static int CompareTypeRepresentation(
        ITypeSymbol left,
        ITypeSymbol right)
    {
        var leftPreference = BuildRepresentationPreference(left);
        var rightPreference = BuildRepresentationPreference(right);
        var comparison = leftPreference.DynamicCount.CompareTo(
            rightPreference.DynamicCount);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = leftPreference.NullableReferenceCount.CompareTo(
            rightPreference.NullableReferenceCount);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = leftPreference.NamedTupleElementCount.CompareTo(
            rightPreference.NamedTupleElementCount);

        return comparison != 0
            ? comparison
            : StringComparer.Ordinal.Compare(
                left.ToDisplayString(
                    SymbolDisplayFormats.FullyQualifiedNullable),
                right.ToDisplayString(
                    SymbolDisplayFormats.FullyQualifiedNullable));
    }

    private static TypeRepresentationPreference BuildRepresentationPreference(
        ITypeSymbol type)
    {
        if (type is IDynamicTypeSymbol)
        {
            return new TypeRepresentationPreference(1, 0, 0);
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return BuildRepresentationPreference(arrayType.ElementType);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return default;
        }

        var result = new TypeRepresentationPreference(
            0,
            namedType.IsReferenceType &&
            namedType.NullableAnnotation == NullableAnnotation.Annotated
                ? 1
                : 0,
            namedType.IsTupleType
                ? namedType.TupleElements.Count(static element =>
                    element.IsExplicitlyNamedTupleElement)
                : 0);

        if (namedType.ContainingType is { } containingType)
        {
            result += BuildRepresentationPreference(containingType);
        }

        foreach (var typeArgument in namedType.TypeArguments)
        {
            result += BuildRepresentationPreference(typeArgument);
        }

        return result;
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
                ConstructionSurfaceNaming.BuildPlanNamespace(
                    definition.Value);
            var planTypeName =
                ConstructionSurfaceNaming.BuildConstructionTypeName(
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

    private readonly record struct ConstructionSurfaceRequest(
        string HintName,
        string Source);

    private readonly record struct TypeRepresentationPreference(
        int DynamicCount,
        int NullableReferenceCount,
        int NamedTupleElementCount)
    {
        public static TypeRepresentationPreference operator +(
            TypeRepresentationPreference left,
            TypeRepresentationPreference right)
        {
            return new TypeRepresentationPreference(
                left.DynamicCount + right.DynamicCount,
                left.NullableReferenceCount +
                right.NullableReferenceCount,
                left.NamedTupleElementCount +
                right.NamedTupleElementCount);
        }
    }
}
