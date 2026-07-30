using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal sealed record KnownSymbols(
    INamedTypeSymbol TypeMapper,
    INamedTypeSymbol MapperBuilder,
    INamedTypeSymbol MapperBuilderBase,
    IMethodSymbol TypeMapperConfigure)
{
    public static KnownSymbols? TryCreate(
        Compilation compilation)
    {
        var typeMapper = compilation.GetTypeByMetadataName(
            MetadataNames.TypeMapper);

        var mapperBuilder = compilation.GetTypeByMetadataName(
            MetadataNames.MapperBuilder);

        var mapperBuilderBase = compilation.GetTypeByMetadataName(
            MetadataNames.MapperBuilderBase);

        if (typeMapper is null ||
            mapperBuilder is null ||
            mapperBuilderBase is null)
        {
            return null;
        }

        var configureMethod = typeMapper
            .GetMembers("Configure")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method =>
                method.MethodKind == MethodKind.Ordinary &&
                !method.IsStatic &&
                method.ReturnsVoid &&
                method.TypeParameters.Length == 0 &&
                method.Parameters.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(
                    method.Parameters[0].Type,
                    mapperBuilder));

        if (configureMethod is null)
        {
            return null;
        }

        return new KnownSymbols(
            typeMapper,
            mapperBuilder,
            mapperBuilderBase,
            configureMethod);
    }
}
