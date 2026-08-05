using System.Collections.Immutable;
using Morphant.Generator.Settings;

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
    string NonNullSourceTypeName,
    string NonNullSourceName,
    string DestinationTypeName,
    string MaybeNullDestinationTypeName,
    string NonNullDestinationTypeName,
    string ResultLocalName,
    bool SourceCanBeNull,
    bool SourceIsNullableValue,
    bool DestinationCanBeNull,
    string? MapNewDirectExpression,
    string? MapExistingDirectExpression,
    TypeMapperFactoryMappingModel? MapNewFactory,
    TypeMapperConstructorMappingModel? MapNewConstructor,
    TypeMapperMapExistingKind MapExistingKind,
    ImmutableArray<TypeMapperMemberMappingModel> MapNewMemberMappings,
    ImmutableArray<TypeMapperMemberMappingModel> MapNewPostMemberMappings,
    ImmutableArray<TypeMapperMemberMappingModel> MapExistingMemberMappings,
    TypeMapperControlFlowMappingModel? ControlFlow = null,
    string? MapNewUnsupportedExceptionMessage = null,
    string? MapExistingUnsupportedExceptionMessage = null,
    string? UnsupportedExceptionMessage = null,
    TypeMapperMemberControlFlowNode? PostMemberControlFlow = null,
    EffectiveMappingSettings EffectiveSettings = default,
    string? MapNewImplMethodName = null,
    string? MapExistingImplMethodName = null,
    ImmutableArray<string> HelperMethodDeclarations = default
)
{
    public string InterfaceTypeName =>
        $"global::Morphant.ITypeMapper<{SourceTypeName}, {DestinationTypeName}>";
}

internal readonly record struct TypeMapperFactoryMappingModel
(
    string ValueExpression,
    string DestinationLocalName,
    string? NullableValueLocalName,
    bool DestinationRequiresNullForgivingOperator,
    bool RequiresNullGuard = false
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
    bool RequiresPreviousDestinationValueLocal = false,
    bool IsResultDependent = false
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
    bool SwitchCanPassUnmatchedValue = true,
    string? EvaluationExpression = null,
    TypeMapperControlFlowNode? EvaluationContinuation = null
);

internal readonly record struct TypeMapperSwitchSectionModel
(
    ImmutableArray<string> Labels,
    TypeMapperControlFlowNode Branch
);

internal sealed record TypeMapperMemberControlFlowNode
(
    ImmutableArray<TypeMapperLocalValueModel> Locals,
    string? Condition,
    TypeMapperMemberControlFlowNode? WhenTrue,
    TypeMapperMemberControlFlowNode? WhenFalse,
    ImmutableArray<TypeMapperMemberMappingModel> MemberMappings,
    string? ThrowExpression,
    string? UnsupportedExceptionMessage = null,
    string? SwitchExpression = null,
    ImmutableArray<TypeMapperMemberSwitchSectionModel> SwitchSections =
        default,
    TypeMapperMemberControlFlowNode? SwitchContinuation = null,
    bool SwitchRequiresFallback = false,
    bool SwitchCanPassUnmatchedValue = true,
    string? EvaluationExpression = null,
    TypeMapperMemberControlFlowNode? EvaluationContinuation = null
);

internal readonly record struct TypeMapperMemberSwitchSectionModel
(
    ImmutableArray<string> Labels,
    TypeMapperMemberControlFlowNode Branch
);
