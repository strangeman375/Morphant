using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal sealed record KnownSymbols
{
    public INamedTypeSymbol TypeMapper { get; }

    public INamedTypeSymbol MapperBuilder { get; }

    public IMethodSymbol TypeMapperConfigure { get; }

    public KnownSymbols(Compilation compilation)
    {
        TypeMapper = compilation.GetTypeByMetadataName("Morphant.TypeMapper")!;
        MapperBuilder = compilation.GetTypeByMetadataName("Morphant.MapperBuilder")!;
        TypeMapperConfigure = TypeMapper
            .GetMembers("Configure")
            .OfType<IMethodSymbol>()
            .Single();
    }
}
