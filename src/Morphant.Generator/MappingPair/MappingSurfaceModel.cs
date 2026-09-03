using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal enum MappingSurfaceKind
{
    Shared,
    MapperScoped,
    MapperFamilyScoped
}

internal readonly record struct MappingSurfaceModel(
    MappingSurfaceKind Kind,
    INamedTypeSymbol DeclaringMapperType,
    ITypeSymbol MapperSelfType)
{
    public string CoordinationIdentity => Kind switch
    {
        MappingSurfaceKind.Shared => "shared",
        MappingSurfaceKind.MapperScoped =>
            "mapper|" + BuildMapperIdentity(),
        MappingSurfaceKind.MapperFamilyScoped =>
            "family|" + BuildMapperIdentity(),
        _ => throw new InvalidOperationException(
            "Unknown mapping surface kind.")
    };

    public string ReadableScopeIdentity => Kind switch
    {
        MappingSurfaceKind.Shared => string.Empty,
        MappingSurfaceKind.MapperScoped =>
            MapperSelfType.ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable),
        MappingSurfaceKind.MapperFamilyScoped =>
            DeclaringMapperType.ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable),
        _ => throw new InvalidOperationException(
            "Unknown mapping surface kind.")
    };

    private string BuildMapperIdentity()
    {
        var definition = DeclaringMapperType.OriginalDefinition;

        return definition.ContainingAssembly.Identity + "|" +
               SymbolNameHelper.GetFullMetadataName(definition);
    }
}

internal static class MappingSurfacePolicy
{
    public static MappingSurfaceModel Create(
        MappingPairModel pair,
        INamedTypeSymbol declaringMapperType)
    {
        var mapperSelfType = FindMapperSelfType(declaringMapperType) ??
            throw new InvalidOperationException(
                "A mapping declaration must derive from TypeMapper<TMapper>.");
        var containsTypeParameter =
            ContainsTypeParameter(pair.SourceType) ||
            ContainsTypeParameter(pair.DestinationType);
        var containsValueTuple =
            ContainsValueTuple(pair.SourceType) ||
            ContainsValueTuple(pair.DestinationType);
        var kind = containsTypeParameter ||
                   mapperSelfType is ITypeParameterSymbol
            ? MappingSurfaceKind.MapperFamilyScoped
            : containsValueTuple
                ? MappingSurfaceKind.MapperScoped
                : MappingSurfaceKind.Shared;

        return new MappingSurfaceModel(
            kind,
            declaringMapperType,
            mapperSelfType);
    }

    private static ITypeSymbol? FindMapperSelfType(
        INamedTypeSymbol mapperType)
    {
        for (var current = mapperType.BaseType;
             current is not null;
             current = current.BaseType)
        {
            if (StringComparer.Ordinal.Equals(
                    SymbolNameHelper.GetFullMetadataName(
                        current.OriginalDefinition),
                    MetadataNames.TypeMapper))
            {
                return current.TypeArguments[0];
            }
        }

        return null;
    }

    private static bool ContainsTypeParameter(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol)
        {
            return true;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return ContainsTypeParameter(arrayType.ElementType);
        }

        if (type is IPointerTypeSymbol pointerType)
        {
            return ContainsTypeParameter(pointerType.PointedAtType);
        }

        if (type is IFunctionPointerTypeSymbol functionPointerType)
        {
            return ContainsTypeParameter(
                       functionPointerType.Signature.ReturnType) ||
                   functionPointerType.Signature.Parameters.Any(
                       static parameter =>
                           ContainsTypeParameter(parameter.Type));
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        return namedType.ContainingType is { } containingType &&
               ContainsTypeParameter(containingType) ||
               namedType.TypeArguments.Any(ContainsTypeParameter);
    }

    private static bool ContainsValueTuple(ITypeSymbol type)
    {
        if (BclTupleShapePolicy.TryCreate(type) is
            { IsValueTuple: true })
        {
            return true;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return ContainsValueTuple(arrayType.ElementType);
        }

        if (type is IPointerTypeSymbol pointerType)
        {
            return ContainsValueTuple(pointerType.PointedAtType);
        }

        if (type is IFunctionPointerTypeSymbol functionPointerType)
        {
            return ContainsValueTuple(
                       functionPointerType.Signature.ReturnType) ||
                   functionPointerType.Signature.Parameters.Any(
                       static parameter =>
                           ContainsValueTuple(parameter.Type));
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        return namedType.ContainingType is { } containingType &&
               ContainsValueTuple(containingType) ||
               namedType.TypeArguments.Any(ContainsValueTuple);
    }
}
