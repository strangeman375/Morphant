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
    string? MapNewDirectExpression,
    string? MapExistingDirectExpression,
    TypeMapperConstructorMappingModel? MapNewConstructor,
    TypeMapperMapExistingKind MapExistingKind,
    string? MapExistingDestinationLocalName,
    ImmutableArray<TypeMapperMemberMappingModel> MapNewMemberMappings,
    ImmutableArray<TypeMapperMemberMappingModel> MapExistingMemberMappings
)
{
    public string InterfaceTypeName =>
        $"global::Morphant.ITypeMapper<{SourceTypeName}, {DestinationTypeName}>";
}

internal readonly record struct TypeMapperConstructorMappingModel
(
    string ConstructedTypeName,
    ImmutableArray<TypeMapperConstructorArgumentMappingModel> Arguments
);

internal enum TypeMapperMapExistingKind
{
    Unsupported,
    Reference,
    Value,
    NullableValue
}

internal readonly record struct TypeMapperConstructorArgumentMappingModel
(
    string ParameterName,
    string SourceMemberName,
    string? ValueLocalName,
    string? ExplicitValueExpression = null,
    string? ValueLocalTypeName = null
);

internal readonly record struct TypeMapperMemberMappingModel
(
    string SourceMemberName,
    string DestinationMemberName,
    bool IsRequired,
    string? SourceValueLocalName,
    string? ExplicitValueExpression = null
);
