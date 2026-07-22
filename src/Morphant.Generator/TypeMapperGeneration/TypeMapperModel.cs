using System.Collections.Immutable;

namespace Morphant.Generator.TypeMapperGeneration;

internal readonly record struct TypeMapperModel
(
    string Namespace,
    ImmutableArray<TypeMapperContainingTypeModel> ContainingTypes,
    string Accessibility,
    string TypeName,
    string TypeParameterList,
    ImmutableArray<TypeMapperMappingModel> Mappings
);

internal readonly record struct TypeMapperContainingTypeModel
(
    string DeclarationKind,
    string TypeName,
    string TypeParameterList
);

internal readonly record struct TypeMapperMappingModel
(
    string SourceTypeName,
    string MaybeNullSourceTypeName,
    string DestinationTypeName,
    string MaybeNullDestinationTypeName,
    bool CanMapNewWithParameterlessConstructor,
    bool CanMapExistingWithoutMembers
)
{
    public string InterfaceTypeName =>
        $"global::Morphant.ITypeMapper<{SourceTypeName}, {DestinationTypeName}>";
}
