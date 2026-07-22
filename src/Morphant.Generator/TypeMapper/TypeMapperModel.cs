using System.Collections.Immutable;

namespace Morphant.Generator.TypeMapperGeneration;

internal readonly record struct TypeMapperModel
(
    string Namespace,
    string Accessibility,
    string TypeName,
    ImmutableArray<TypeMapperMappingModel> Mappings
);

internal readonly record struct TypeMapperMappingModel
(
    string SourceTypeName,
    string DestinationTypeName
)
{
    public string InterfaceTypeName =>
        $"global::Morphant.ITypeMapper<{SourceTypeName}, {DestinationTypeName}>";
}
