using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal sealed record KnownSymbols(
    INamedTypeSymbol TypeMapper,
    INamedTypeSymbol TypeMapperInterface,
    INamedTypeSymbol MapperBuilder,
    INamedTypeSymbol MapperBuilderBase,
    IMethodSymbol TypeMapperConfigure,
    INamedTypeSymbol SystemType)
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

        var typeMapperInterface = compilation.GetTypeByMetadataName(
            MetadataNames.TypeMapperInterface);

        var systemType = compilation.GetTypeByMetadataName(
            MetadataNames.SystemType);

        if (typeMapper is null ||
            typeMapperInterface is null ||
            mapperBuilder is null ||
            mapperBuilderBase is null ||
            systemType is null)
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
            typeMapperInterface,
            mapperBuilder,
            mapperBuilderBase,
            configureMethod,
            systemType);
    }
}
