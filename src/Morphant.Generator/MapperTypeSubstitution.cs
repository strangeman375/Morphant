using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal static class MapperTypeSubstitution
{
    public static Dictionary<ITypeParameterSymbol, ITypeSymbol> Build(
        INamedTypeSymbol declaredType,
        INamedTypeSymbol constructedType)
    {
        var result =
            new Dictionary<ITypeParameterSymbol, ITypeSymbol>(
                SymbolEqualityComparer.Default);
        AddTypeAndContainingTypes(
            declaredType,
            constructedType,
            result);
        return result;
    }

    public static Dictionary<ITypeParameterSymbol, ITypeSymbol>
        BuildForHierarchy(INamedTypeSymbol mapperType)
    {
        var result =
            new Dictionary<ITypeParameterSymbol, ITypeSymbol>(
                SymbolEqualityComparer.Default);

        for (var current = mapperType;
             current is not null;
             current = current.BaseType)
        {
            AddTypeAndContainingTypes(
                current.OriginalDefinition,
                current,
                result);
        }

        return result;
    }

    public static ITypeSymbol Substitute(
        ITypeSymbol type,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol> substitutions,
        Compilation compilation)
    {
        if (type is ITypeParameterSymbol typeParameter &&
            substitutions.TryGetValue(typeParameter, out var substitution))
        {
            return type.NullableAnnotation == NullableAnnotation.Annotated
                ? substitution.WithNullableAnnotation(
                    NullableAnnotation.Annotated)
                : substitution;
        }

        if (type is IArrayTypeSymbol array)
        {
            var element = Substitute(
                array.ElementType,
                substitutions,
                compilation);

            return SymbolEqualityComparer.Default.Equals(
                    element,
                    array.ElementType)
                ? type
                : compilation.CreateArrayTypeSymbol(element, array.Rank)
                    .WithNullableAnnotation(type.NullableAnnotation);
        }

        if (type is not INamedTypeSymbol named)
        {
            return type;
        }

        var containingType = named.ContainingType is null
            ? null
            : (INamedTypeSymbol)Substitute(
                named.ContainingType,
                substitutions,
                compilation);
        var arguments = named.TypeArguments
            .Select(argument =>
                Substitute(argument, substitutions, compilation))
            .ToArray();
        var containingChanged = !SymbolEqualityComparer.Default.Equals(
            containingType,
            named.ContainingType);
        var argumentsChanged = !arguments.SequenceEqual(
            named.TypeArguments,
            SymbolEqualityComparer.Default);

        if (!containingChanged && !argumentsChanged)
        {
            return type;
        }

        INamedTypeSymbol definition;

        if (containingChanged && containingType is not null)
        {
            definition = containingType
                .GetTypeMembers(named.Name, named.Arity)
                .First(candidate =>
                    SymbolEqualityComparer.Default.Equals(
                        candidate.OriginalDefinition,
                        named.OriginalDefinition));
        }
        else
        {
            definition = named.ConstructedFrom;
        }

        var constructed = definition.Arity == 0
            ? definition
            : definition.Construct(arguments);

        if (named.IsTupleType)
        {
            constructed = RestoreTuplePresentation(
                named,
                constructed,
                substitutions,
                compilation);
        }

        return constructed.WithNullableAnnotation(
            type.NullableAnnotation);
    }

    private static INamedTypeSymbol RestoreTuplePresentation(
        INamedTypeSymbol original,
        INamedTypeSymbol substitutedUnderlyingType,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol> substitutions,
        Compilation compilation)
    {
        var elements = original.TupleElements;
        var hasExplicitNames = elements.Any(static element =>
            element.IsExplicitlyNamedTupleElement);
        var names = hasExplicitNames
            ? elements.Select(static element =>
                    element.IsExplicitlyNamedTupleElement
                        ? element.Name
                        : (string?)null)
                .ToImmutableArray()
            : ImmutableArray<string?>.Empty;
        var locations = hasExplicitNames
            ? elements.Select(static element =>
                    (Location?)(element.Locations.FirstOrDefault() ??
                        Location.None))
                .ToImmutableArray()
            : ImmutableArray<Location?>.Empty;
        var nullableAnnotations = elements
            .Select(element =>
                Substitute(
                    element.Type,
                    substitutions,
                    compilation).NullableAnnotation)
            .ToImmutableArray();

        return compilation.CreateTupleTypeSymbol(
            substitutedUnderlyingType,
            names,
            locations,
            nullableAnnotations);
    }

    private static void AddTypeAndContainingTypes(
        INamedTypeSymbol declaredType,
        INamedTypeSymbol constructedType,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> result)
    {
        INamedTypeSymbol? declared = declaredType;
        INamedTypeSymbol? constructed = constructedType;

        while (declared is not null && constructed is not null)
        {
            for (var index = 0;
                 index < declared.TypeParameters.Length &&
                 index < constructed.TypeArguments.Length;
                 index++)
            {
                result[declared.TypeParameters[index]] =
                    constructed.TypeArguments[index];
            }

            declared = declared.ContainingType;
            constructed = constructed.ContainingType;
        }
    }
}
