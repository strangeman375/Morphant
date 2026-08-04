using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class CanonicalMappingPairSelector
{
    public static ImmutableArray<MappingPairModel> Select(
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

                var key = MappingTypeIdentityPolicy
                    .CreateAlphaEquivalentPairKey(
                        pair.SourceType,
                        pair.DestinationType);

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
