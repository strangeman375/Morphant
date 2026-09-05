using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal enum MappingSurfaceKind
{
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
        MappingSurfaceKind.MapperScoped =>
            "mapper|" + BuildMapperIdentity(),
        MappingSurfaceKind.MapperFamilyScoped =>
            "family|" + BuildMapperIdentity(),
        _ => throw new InvalidOperationException(
            "Unknown mapping surface kind.")
    };

    public string ReadableScopeIdentity => Kind switch
    {
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
        INamedTypeSymbol declaringMapperType)
    {
        var mapperSelfType = FindMapperSelfType(declaringMapperType) ??
            throw new InvalidOperationException(
                "A mapping declaration must derive from TypeMapper<TMapper>.");
        var kind = mapperSelfType is ITypeParameterSymbol
            ? MappingSurfaceKind.MapperFamilyScoped
            : MappingSurfaceKind.MapperScoped;

        return new MappingSurfaceModel(
            kind,
            declaringMapperType,
            mapperSelfType);
    }

    internal static ITypeSymbol? FindMapperSelfType(
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
}
