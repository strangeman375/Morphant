using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class MapperFamilyTypeParameterPolicy
{
    public static ImmutableArray<ITypeParameterSymbol>
        FindMissingPairParameters(
            INamedTypeSymbol mapperType,
            ITypeSymbol sourceType,
            ITypeSymbol destinationType)
    {
        var mapperSelfType = MappingSurfacePolicy.FindMapperSelfType(
            mapperType);

        if (mapperSelfType is not ITypeParameterSymbol selfParameter)
        {
            return ImmutableArray<ITypeParameterSymbol>.Empty;
        }

        var result = ImmutableArray.CreateBuilder<ITypeParameterSymbol>();

        foreach (var typeParameter in EnumerateTypeParameters(mapperType))
        {
            if (SymbolEqualityComparer.Default.Equals(
                    typeParameter,
                    selfParameter) ||
                Contains(sourceType, typeParameter) ||
                Contains(destinationType, typeParameter))
            {
                continue;
            }

            result.Add(typeParameter);
        }

        return result.ToImmutable();
    }

    private static IEnumerable<ITypeParameterSymbol> EnumerateTypeParameters(
        INamedTypeSymbol mapperType)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();

        for (var current = mapperType;
             current is not null;
             current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        while (containingTypes.Count > 0)
        {
            foreach (var typeParameter in
                     containingTypes.Pop().TypeParameters)
            {
                yield return typeParameter;
            }
        }
    }

    private static bool Contains(
        ITypeSymbol type,
        ITypeParameterSymbol expected)
    {
        if (type is ITypeParameterSymbol typeParameter)
        {
            return SymbolEqualityComparer.Default.Equals(
                typeParameter,
                expected);
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return Contains(arrayType.ElementType, expected);
        }

        if (type is IPointerTypeSymbol pointerType)
        {
            return Contains(pointerType.PointedAtType, expected);
        }

        if (type is IFunctionPointerTypeSymbol functionPointerType)
        {
            return Contains(
                       functionPointerType.Signature.ReturnType,
                       expected) ||
                   functionPointerType.Signature.Parameters.Any(parameter =>
                       Contains(parameter.Type, expected));
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        return namedType.ContainingType is { } containingType &&
               Contains(containingType, expected) ||
               namedType.TypeArguments.Any(argument =>
                   Contains(argument, expected));
    }
}
