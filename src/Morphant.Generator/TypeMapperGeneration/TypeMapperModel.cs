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
    TypeMapperFactoryMappingModel? MapNewFactory,
    TypeMapperConstructorMappingModel? MapNewConstructor,
    TypeMapperMapExistingKind MapExistingKind,
    string? MapExistingDestinationLocalName,
    ImmutableArray<TypeMapperMemberMappingModel> MapNewMemberMappings,
    ImmutableArray<TypeMapperMemberMappingModel> MapExistingMemberMappings,
    TypeMapperControlFlowMappingModel? ControlFlow = null,
    string? MapNewUnsupportedExceptionMessage = null,
    string? MapExistingUnsupportedExceptionMessage = null,
    string? UnsupportedExceptionMessage = null
)
{
    public string InterfaceTypeName =>
        $"global::Morphant.ITypeMapper<{SourceTypeName}, {DestinationTypeName}>";
}

internal readonly record struct TypeMapperFactoryMappingModel
(
    string? LocalFunctionName,
    string? LocalFunctionDeclaration,
    string ValueExpression,
    TypeMapperFactoryDelegateModel? Delegate,
    ImmutableArray<string> RuntimeLocalDependencies,
    string DestinationLocalName,
    string? NullableValueLocalName
);

internal readonly record struct TypeMapperFactoryDelegateModel
(
    string TypeName,
    string LocalName,
    string ValueExpression
);

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
    string? ValueLocalTypeName = null,
    string? TargetTypeName = null
);

internal readonly record struct TypeMapperMemberMappingModel
(
    string SourceMemberName,
    string DestinationMemberName,
    bool IsRequired,
    string? SourceValueLocalName,
    string? ExplicitValueExpression = null,
    string? ExplicitValueTypeName = null,
    string? ValueLocalName = null,
    bool RequiresPreviousDestinationValueLocal = false
);

internal sealed record TypeMapperControlFlowMappingModel
(
    TypeMapperControlFlowNode MapNewRoot,
    TypeMapperControlFlowNode MapExistingRoot
);

internal readonly record struct TypeMapperLocalValueModel
(
    string DeclarationType,
    string Name,
    string ValueExpression,
    bool IsConst,
    bool IsSynthetic = false
);

internal sealed record TypeMapperControlFlowNode
(
    ImmutableArray<TypeMapperLocalValueModel> Locals,
    string? Condition,
    TypeMapperControlFlowNode? WhenTrue,
    TypeMapperControlFlowNode? WhenFalse,
    TypeMapperMappingModel? Leaf,
    string? ThrowExpression,
    string? SwitchExpression = null,
    ImmutableArray<TypeMapperSwitchSectionModel> SwitchSections = default,
    TypeMapperControlFlowNode? SwitchContinuation = null,
    bool SwitchRequiresFallback = false,
    bool SwitchCanPassUnmatchedValue = true
);

internal readonly record struct TypeMapperSwitchSectionModel
(
    ImmutableArray<string> Labels,
    TypeMapperControlFlowNode Branch
);
