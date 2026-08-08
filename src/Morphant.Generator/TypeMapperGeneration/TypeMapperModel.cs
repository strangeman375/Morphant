using System.Collections.Immutable;
using Morphant.Generator.PairConfiguration;
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
    string SourceRuntimeTypeName,
    string MaybeNullSourceTypeName,
    string NonNullSourceTypeName,
    string NonNullSourceName,
    string DestinationTypeName,
    string DestinationRuntimeTypeName,
    string MaybeNullDestinationTypeName,
    string NonNullDestinationTypeName,
    string ResultLocalName,
    bool SourceCanBeNull,
    bool SourceIsNullableValue,
    bool DestinationCanBeNull,
    string? CreateDirectExpression,
    string? UpdateDirectExpression,
    TypeMapperFactoryMappingModel? CreateFactory,
    TypeMapperConstructorMappingModel? CreateConstructor,
    TypeMapperUpdateKind UpdateKind,
    ImmutableArray<TypeMapperMemberMappingModel> CreateMemberMappings,
    ImmutableArray<TypeMapperMemberMappingModel> CreatePostMemberMappings,
    ImmutableArray<TypeMapperMemberMappingModel> UpdateMemberMappings,
    TypeMapperControlFlowMappingModel? ControlFlow = null,
    TypeMapperManualMappingModel? ManualMapping = null,
    string? CreateUnsupportedExceptionMessage = null,
    string? UpdateUnsupportedExceptionMessage = null,
    string? UnsupportedExceptionMessage = null,
    TypeMapperMemberControlFlowNode? PostMemberControlFlow = null,
    EffectiveMappingSettings EffectiveSettings = default,
    string? CreateImplMethodName = null,
    string? UpdateImplMethodName = null,
    ImmutableArray<string> HelperMethodDeclarations = default
)
{
    public string InterfaceTypeName =>
        $"global::Morphant.ITypeMapper<{SourceTypeName}, {DestinationTypeName}>";
}

internal readonly record struct TypeMapperManualMappingModel
(
    string? HelperMethodName,
    ConvertConfigurationForm Form
);

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
    ImmutableArray<TypeMapperConstructorArgumentMappingModel> Arguments,
    ImmutableArray<TypeMapperLocalValueModel> ValueLocals = default
);

internal enum TypeMapperUpdateKind
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
    string? TargetTypeName = null,
    TypeMapperDependencyExpressionModel? DependencyExpression = null,
    ImmutableArray<TypeMapperLocalValueModel> EvaluationLocals = default
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
    bool IsResultDependent = false,
    TypeMapperDependencyExpressionModel? DependencyExpression = null,
    ImmutableArray<TypeMapperLocalValueModel> EvaluationLocals = default
);

internal sealed record TypeMapperControlFlowMappingModel
(
    TypeMapperControlFlowNode CreateRoot,
    TypeMapperControlFlowNode UpdateRoot
);

internal readonly record struct TypeMapperLocalValueModel
(
    string DeclarationType,
    string Name,
    string ValueExpression,
    bool IsConst,
    bool IsSynthetic = false,
    TypeMapperDependencyExpressionModel? DependencyExpression = null,
    string? DeclaredValueKey = null,
    string? StoredValueTypeName = null
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
    TypeMapperControlFlowNode? EvaluationContinuation = null,
    TypeMapperDependencyExpressionModel? ConditionDependency = null,
    TypeMapperDependencyExpressionModel? ThrowDependency = null,
    TypeMapperDependencyExpressionModel? SwitchDependency = null,
    TypeMapperDependencyExpressionModel? EvaluationDependency = null
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
    TypeMapperMemberControlFlowNode? EvaluationContinuation = null,
    TypeMapperDependencyExpressionModel? ConditionDependency = null,
    TypeMapperDependencyExpressionModel? ThrowDependency = null,
    TypeMapperDependencyExpressionModel? SwitchDependency = null,
    TypeMapperDependencyExpressionModel? EvaluationDependency = null
);

internal readonly record struct TypeMapperMemberSwitchSectionModel
(
    ImmutableArray<string> Labels,
    TypeMapperMemberControlFlowNode Branch
);

internal sealed record TypeMapperDependencyExpressionModel
(
    TypeMapperDependencyExpressionNodeModel Root
)
{
    public string Render() => Root.Render();
}

internal sealed record TypeMapperDependencyExpressionNodeModel
(
    string Key,
    string ValueTypeName,
    bool CanMaterialize,
    string ExpressionTemplate,
    ImmutableArray<TypeMapperDependencyExpressionChildModel> Children
)
{
    public string Render()
    {
        var result = ExpressionTemplate;

        foreach (var child in Children)
        {
            result = result.Replace(
                child.Placeholder,
                child.Node.Render());
        }

        return result;
    }
}

internal readonly record struct TypeMapperDependencyExpressionChildModel
(
    string Placeholder,
    TypeMapperDependencyExpressionNodeModel Node
);

internal readonly record struct TypeMapperRewrittenDependencyExpression
(
    string Expression,
    TypeMapperDependencyExpressionModel? DependencyExpression
);
