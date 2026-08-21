using System.Collections.Immutable;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TypeMapperRuntimeEquality
{
    public static bool AreEquivalent(
        TypeMapperControlFlowNode left,
        TypeMapperControlFlowNode right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return AreEquivalent(left.Locals, right.Locals) &&
               StringComparer.Ordinal.Equals(
                   left.Condition,
                   right.Condition) &&
               AreEquivalentNode(left.WhenTrue, right.WhenTrue) &&
               AreEquivalentNode(left.WhenFalse, right.WhenFalse) &&
               AreEquivalent(left.Leaf, right.Leaf) &&
               StringComparer.Ordinal.Equals(
                   left.ThrowExpression,
                   right.ThrowExpression) &&
               StringComparer.Ordinal.Equals(
                   left.SwitchExpression,
                   right.SwitchExpression) &&
               AreEquivalent(
                   left.SwitchSections,
                   right.SwitchSections) &&
               AreEquivalentNode(
                   left.SwitchContinuation,
                   right.SwitchContinuation) &&
               left.SwitchRequiresFallback ==
                   right.SwitchRequiresFallback &&
               left.SwitchCanPassUnmatchedValue ==
                   right.SwitchCanPassUnmatchedValue &&
               StringComparer.Ordinal.Equals(
                   left.EvaluationExpression,
                   right.EvaluationExpression) &&
               AreEquivalentNode(
                   left.EvaluationContinuation,
                   right.EvaluationContinuation) &&
               AreEquivalent(
                   left.ConditionDependency,
                   right.ConditionDependency) &&
               AreEquivalent(
                   left.ThrowDependency,
                   right.ThrowDependency) &&
               AreEquivalent(
                   left.SwitchDependency,
                   right.SwitchDependency) &&
               AreEquivalent(
                   left.EvaluationDependency,
                   right.EvaluationDependency) &&
               left.ThrowUsesCurrentMappingOperation ==
                   right.ThrowUsesCurrentMappingOperation;
    }

    public static bool AreEquivalent(
        TypeMapperConstructorMappingModel left,
        TypeMapperConstructorMappingModel right) =>
        StringComparer.Ordinal.Equals(
            left.ConstructedTypeName,
            right.ConstructedTypeName) &&
        AreEquivalent(left.Arguments, right.Arguments) &&
        AreEquivalent(left.ValueLocals, right.ValueLocals);

    public static bool AreEquivalent(
        ImmutableArray<TypeMapperMemberMappingModel> left,
        ImmutableArray<TypeMapperMemberMappingModel> right)
    {
        var length = Count(left);

        if (length != Count(right))
        {
            return false;
        }

        for (var index = 0; index < length; index++)
        {
            if (!AreEquivalent(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalentNode(
        TypeMapperControlFlowNode? left,
        TypeMapperControlFlowNode? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left is not null &&
               right is not null &&
               AreEquivalent(left, right);
    }

    private static bool AreEquivalent(
        TypeMapperMappingModel? left,
        TypeMapperMappingModel? right)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return left.HasValue == right.HasValue;
        }

        var leftMapping = left.Value;
        var rightMapping = right.Value;

        return StringComparer.Ordinal.Equals(
                   leftMapping.SourceTypeName,
                   rightMapping.SourceTypeName) &&
               StringComparer.Ordinal.Equals(
                   leftMapping.SourceRuntimeTypeName,
                   rightMapping.SourceRuntimeTypeName) &&
               StringComparer.Ordinal.Equals(
                   leftMapping.MaybeNullSourceTypeName,
                   rightMapping.MaybeNullSourceTypeName) &&
               StringComparer.Ordinal.Equals(
                   leftMapping.NonNullSourceTypeName,
                   rightMapping.NonNullSourceTypeName) &&
               StringComparer.Ordinal.Equals(
                   leftMapping.NonNullSourceName,
                   rightMapping.NonNullSourceName) &&
               StringComparer.Ordinal.Equals(
                   leftMapping.DestinationTypeName,
                   rightMapping.DestinationTypeName) &&
               StringComparer.Ordinal.Equals(
                   leftMapping.DestinationRuntimeTypeName,
                   rightMapping.DestinationRuntimeTypeName) &&
               StringComparer.Ordinal.Equals(
                   leftMapping.MaybeNullDestinationTypeName,
                   rightMapping.MaybeNullDestinationTypeName) &&
               StringComparer.Ordinal.Equals(
                   leftMapping.NonNullDestinationTypeName,
                   rightMapping.NonNullDestinationTypeName) &&
               StringComparer.Ordinal.Equals(
                   leftMapping.ResultLocalName,
                   rightMapping.ResultLocalName) &&
               leftMapping.SourceCanBeNull ==
                   rightMapping.SourceCanBeNull &&
               leftMapping.SourceIsNullableValue ==
                   rightMapping.SourceIsNullableValue &&
               leftMapping.DestinationCanBeNull ==
                   rightMapping.DestinationCanBeNull &&
               StringComparer.Ordinal.Equals(
                   leftMapping.CreateDirectExpression,
                   rightMapping.CreateDirectExpression) &&
               StringComparer.Ordinal.Equals(
                   leftMapping.UpdateDirectExpression,
                   rightMapping.UpdateDirectExpression) &&
               Equals(
                   leftMapping.CreateFactory,
                   rightMapping.CreateFactory) &&
               AreEquivalent(
                   leftMapping.CreateConstructor,
                   rightMapping.CreateConstructor) &&
               leftMapping.UpdateKind == rightMapping.UpdateKind &&
               AreEquivalent(
                   leftMapping.CreateMemberMappings,
                   rightMapping.CreateMemberMappings) &&
               AreEquivalent(
                   leftMapping.CreatePostMemberMappings,
                   rightMapping.CreatePostMemberMappings) &&
               AreEquivalent(
                   leftMapping.UpdateMemberMappings,
                   rightMapping.UpdateMemberMappings) &&
               AreEquivalent(
                   leftMapping.ControlFlow,
                   rightMapping.ControlFlow) &&
               Equals(
                   leftMapping.ManualMapping,
                   rightMapping.ManualMapping) &&
               AreEquivalent(
                   leftMapping.CreateFailure,
                   rightMapping.CreateFailure) &&
               AreEquivalent(
                   leftMapping.UpdateFailure,
                   rightMapping.UpdateFailure) &&
               AreEquivalent(
                   leftMapping.CreateOperationFailure,
                   rightMapping.CreateOperationFailure) &&
               AreEquivalent(
                   leftMapping.UpdateOperationFailure,
                   rightMapping.UpdateOperationFailure) &&
               AreEquivalent(
                   leftMapping.Failure,
                   rightMapping.Failure) &&
               AreEquivalentNestedObservations(
                   leftMapping.NestedObservations,
                   rightMapping.NestedObservations) &&
               AreEquivalentNestedObservations(
                   leftMapping.MemberObservation?.NestedMappings ?? ImmutableArray<NestedMappingObservation>.Empty,
                   rightMapping.MemberObservation?.NestedMappings ?? ImmutableArray<NestedMappingObservation>.Empty) &&
               AreEquivalent(
                   leftMapping.DerivedMappings,
                   rightMapping.DerivedMappings) &&
               AreEquivalent(
                   leftMapping.PostMemberControlFlow,
                   rightMapping.PostMemberControlFlow) &&
               leftMapping.EffectiveSettings.Equals(
                   rightMapping.EffectiveSettings) &&
               StringComparer.Ordinal.Equals(
                   leftMapping.CreateImplMethodName,
                   rightMapping.CreateImplMethodName) &&
               StringComparer.Ordinal.Equals(
                   leftMapping.UpdateImplMethodName,
                   rightMapping.UpdateImplMethodName) &&
               leftMapping.CreateImplUsesOperation ==
                   rightMapping.CreateImplUsesOperation &&
               AreEquivalent(
                   leftMapping.HelperMethodDeclarations,
                   rightMapping.HelperMethodDeclarations) &&
               AreEquivalent(
                   leftMapping.TransferredWarningSuppressions,
                   rightMapping.TransferredWarningSuppressions) &&
               leftMapping.RequiresUnsafeContext ==
                   rightMapping.RequiresUnsafeContext;
    }

    private static bool AreEquivalent(
        TypeMapperConstructorMappingModel? left,
        TypeMapperConstructorMappingModel? right)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return left.HasValue == right.HasValue;
        }

        return AreEquivalent(left.Value, right.Value);
    }

    private static bool AreEquivalent(
        TypeMapperControlFlowMappingModel? left,
        TypeMapperControlFlowMappingModel? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left is not null &&
               right is not null &&
               AreEquivalent(left.CreateRoot, right.CreateRoot) &&
               AreEquivalent(left.UpdateRoot, right.UpdateRoot);
    }

    private static bool AreEquivalent(
        ImmutableArray<TypeMapperSwitchSectionModel> left,
        ImmutableArray<TypeMapperSwitchSectionModel> right)
    {
        var length = Count(left);

        if (length != Count(right))
        {
            return false;
        }

        for (var index = 0; index < length; index++)
        {
            if (!AreEquivalent(
                    left[index].Labels,
                    right[index].Labels) ||
                !AreEquivalent(
                    left[index].Branch,
                    right[index].Branch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalent(
        TypeMapperMemberControlFlowNode? left,
        TypeMapperMemberControlFlowNode? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return AreEquivalent(left.Locals, right.Locals) &&
               StringComparer.Ordinal.Equals(
                   left.Condition,
                   right.Condition) &&
               AreEquivalent(left.WhenTrue, right.WhenTrue) &&
               AreEquivalent(left.WhenFalse, right.WhenFalse) &&
               AreEquivalent(
                   left.MemberMappings,
                   right.MemberMappings) &&
               StringComparer.Ordinal.Equals(
                   left.ThrowExpression,
                   right.ThrowExpression) &&
               AreEquivalent(left.Failure, right.Failure) &&
               StringComparer.Ordinal.Equals(
                   left.SwitchExpression,
                   right.SwitchExpression) &&
               AreEquivalent(
                   left.SwitchSections,
                   right.SwitchSections) &&
               AreEquivalent(
                   left.SwitchContinuation,
                   right.SwitchContinuation) &&
               left.SwitchRequiresFallback ==
                   right.SwitchRequiresFallback &&
               left.SwitchCanPassUnmatchedValue ==
                   right.SwitchCanPassUnmatchedValue &&
               StringComparer.Ordinal.Equals(
                   left.EvaluationExpression,
                   right.EvaluationExpression) &&
               AreEquivalent(
                   left.EvaluationContinuation,
                   right.EvaluationContinuation) &&
               AreEquivalent(
                   left.ConditionDependency,
                   right.ConditionDependency) &&
               AreEquivalent(
                   left.ThrowDependency,
                   right.ThrowDependency) &&
               AreEquivalent(
                   left.SwitchDependency,
                   right.SwitchDependency) &&
               AreEquivalent(
                   left.EvaluationDependency,
                   right.EvaluationDependency) &&
               left.ThrowUsesCurrentMappingOperation ==
                   right.ThrowUsesCurrentMappingOperation &&
               AreEquivalentNestedObservations(
                   left.MemberObservation?.NestedMappings ?? ImmutableArray<NestedMappingObservation>.Empty,
                   right.MemberObservation?.NestedMappings ?? ImmutableArray<NestedMappingObservation>.Empty);
    }

    private static bool AreEquivalentNestedObservations(
        ImmutableArray<NestedMappingObservation> left,
        ImmutableArray<NestedMappingObservation> right)
    {
        var leftInvalid = InvalidNestedObservations(left);
        var rightInvalid = InvalidNestedObservations(right);

        if (leftInvalid.Length != rightInvalid.Length)
        {
            return false;
        }

        for (var index = 0; index < leftInvalid.Length; index++)
        {
            var leftObservation = leftInvalid[index];
            var rightObservation = rightInvalid[index];

            if (leftObservation.FailureKind !=
                    rightObservation.FailureKind ||
                leftObservation.Paths != rightObservation.Paths ||
                !SameSyntax(
                    leftObservation.Producer,
                    rightObservation.Producer) ||
                !SameSyntax(
                    leftObservation.TargetDesignator,
                    rightObservation.TargetDesignator) ||
                !StringComparer.Ordinal.Equals(
                    leftObservation.TargetName,
                    rightObservation.TargetName) ||
                !StringComparer.Ordinal.Equals(
                    leftObservation.GeneratedCurrentDestination,
                    rightObservation.GeneratedCurrentDestination))
            {
                return false;
            }
        }

        return true;
    }

    private static ImmutableArray<NestedMappingObservation>
        InvalidNestedObservations(
            ImmutableArray<NestedMappingObservation> observations)
    {
        return observations.IsDefaultOrEmpty
            ? ImmutableArray<NestedMappingObservation>.Empty
            : observations.Where(static observation =>
                    observation.FailureKind !=
                        NestedMappingFailureKind.None)
                .ToImmutableArray();
    }

    private static bool SameSyntax(
        Microsoft.CodeAnalysis.SyntaxNode? left,
        Microsoft.CodeAnalysis.SyntaxNode? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left is not null &&
               right is not null &&
               ReferenceEquals(left.SyntaxTree, right.SyntaxTree) &&
               left.Span == right.Span;
    }

    private static bool AreEquivalent(
        ImmutableArray<TypeMapperMemberSwitchSectionModel> left,
        ImmutableArray<TypeMapperMemberSwitchSectionModel> right)
    {
        var length = Count(left);

        if (length != Count(right))
        {
            return false;
        }

        for (var index = 0; index < length; index++)
        {
            if (!AreEquivalent(
                    left[index].Labels,
                    right[index].Labels) ||
                !AreEquivalent(
                    left[index].Branch,
                    right[index].Branch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalent(
        MappingFailureObservation? left,
        MappingFailureObservation? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left is not null &&
               right is not null &&
               StringComparer.Ordinal.Equals(
                   left.RecoveryMessage,
                   right.RecoveryMessage);
    }

    private static bool AreEquivalent(
        ImmutableArray<string> left,
        ImmutableArray<string> right)
    {
        var length = Count(left);

        if (length != Count(right))
        {
            return false;
        }

        for (var index = 0; index < length; index++)
        {
            if (!StringComparer.Ordinal.Equals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalent(
        ImmutableArray<TypeMapperDerivedMappingModel> left,
        ImmutableArray<TypeMapperDerivedMappingModel> right)
    {
        var length = Count(left);

        if (length != Count(right))
        {
            return false;
        }

        for (var index = 0; index < length; index++)
        {
            var leftMapping = left[index];
            var rightMapping = right[index];

            if (!StringComparer.Ordinal.Equals(
                    leftMapping.SourceTypeName,
                    rightMapping.SourceTypeName) ||
                !StringComparer.Ordinal.Equals(
                    leftMapping.SourceRuntimeTypeName,
                    rightMapping.SourceRuntimeTypeName) ||
                !StringComparer.Ordinal.Equals(
                    leftMapping.SourceMatchTypeName,
                    rightMapping.SourceMatchTypeName) ||
                !StringComparer.Ordinal.Equals(
                    leftMapping.DestinationTypeName,
                    rightMapping.DestinationTypeName) ||
                !StringComparer.Ordinal.Equals(
                    leftMapping.DestinationRuntimeTypeName,
                    rightMapping.DestinationRuntimeTypeName) ||
                !StringComparer.Ordinal.Equals(
                    leftMapping.DestinationMatchTypeName,
                    rightMapping.DestinationMatchTypeName) ||
                leftMapping.DestinationCanBeNull !=
                    rightMapping.DestinationCanBeNull ||
                leftMapping.DestinationMatchesBase !=
                    rightMapping.DestinationMatchesBase ||
                !AreEquivalent(
                    leftMapping.MoreSpecificMappingIndexes,
                    rightMapping.MoreSpecificMappingIndexes) ||
                !AreEquivalent(
                    leftMapping.DisqualifyingMappingIndexes,
                    rightMapping.DisqualifyingMappingIndexes))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalent(
        ImmutableArray<int> left,
        ImmutableArray<int> right)
    {
        var length = Count(left);

        if (length != Count(right))
        {
            return false;
        }

        for (var index = 0; index < length; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalent(
        ImmutableArray<TypeMapperConstructorArgumentMappingModel> left,
        ImmutableArray<TypeMapperConstructorArgumentMappingModel> right)
    {
        var length = Count(left);

        if (length != Count(right))
        {
            return false;
        }

        for (var index = 0; index < length; index++)
        {
            var leftArgument = left[index];
            var rightArgument = right[index];

            if (!StringComparer.Ordinal.Equals(
                    leftArgument.ParameterName,
                    rightArgument.ParameterName) ||
                !StringComparer.Ordinal.Equals(
                    leftArgument.SourceMemberName,
                    rightArgument.SourceMemberName) ||
                !StringComparer.Ordinal.Equals(
                    leftArgument.ValueLocalName,
                    rightArgument.ValueLocalName) ||
                !StringComparer.Ordinal.Equals(
                    leftArgument.ExplicitValueExpression,
                    rightArgument.ExplicitValueExpression) ||
                !Equals(
                    leftArgument.ConventionValueExpression,
                    rightArgument.ConventionValueExpression) ||
                !Equals(
                    leftArgument.ConventionProbeValueExpression,
                    rightArgument.ConventionProbeValueExpression) ||
                !StringComparer.Ordinal.Equals(
                    leftArgument.ValueLocalTypeName,
                    rightArgument.ValueLocalTypeName) ||
                !StringComparer.Ordinal.Equals(
                    leftArgument.TargetTypeName,
                    rightArgument.TargetTypeName) ||
                !AreEquivalent(
                    leftArgument.DependencyExpression,
                    rightArgument.DependencyExpression) ||
                !AreEquivalent(
                    leftArgument.EvaluationLocals,
                    rightArgument.EvaluationLocals))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalent(
        TypeMapperMemberMappingModel left,
        TypeMapperMemberMappingModel right)
    {
        return StringComparer.Ordinal.Equals(
                   left.SourceMemberName,
                   right.SourceMemberName) &&
               StringComparer.Ordinal.Equals(
                   left.DestinationMemberName,
                   right.DestinationMemberName) &&
               left.IsRequired == right.IsRequired &&
               StringComparer.Ordinal.Equals(
                   left.SourceValueLocalName,
                   right.SourceValueLocalName) &&
               StringComparer.Ordinal.Equals(
                   left.ExplicitValueExpression,
                   right.ExplicitValueExpression) &&
               StringComparer.Ordinal.Equals(
                   left.ExplicitValueTypeName,
                   right.ExplicitValueTypeName) &&
               StringComparer.Ordinal.Equals(
                   left.ValueLocalName,
                   right.ValueLocalName) &&
               Equals(
                   left.ConventionValueExpression,
                   right.ConventionValueExpression) &&
               left.RequiresPreviousDestinationValueLocal ==
                   right.RequiresPreviousDestinationValueLocal &&
               left.IsResultDependent == right.IsResultDependent &&
               AreEquivalent(
                   left.DependencyExpression,
                   right.DependencyExpression) &&
               AreEquivalent(
                   left.EvaluationLocals,
                   right.EvaluationLocals);
    }

    private static bool AreEquivalent(
        ImmutableArray<TypeMapperLocalValueModel> left,
        ImmutableArray<TypeMapperLocalValueModel> right)
    {
        var length = Count(left);

        if (length != Count(right))
        {
            return false;
        }

        for (var index = 0; index < length; index++)
        {
            var leftLocal = left[index];
            var rightLocal = right[index];

            if (!StringComparer.Ordinal.Equals(
                    leftLocal.DeclarationType,
                    rightLocal.DeclarationType) ||
                !StringComparer.Ordinal.Equals(
                    leftLocal.Name,
                    rightLocal.Name) ||
                !StringComparer.Ordinal.Equals(
                    leftLocal.ValueExpression,
                    rightLocal.ValueExpression) ||
                leftLocal.IsConst != rightLocal.IsConst ||
                leftLocal.IsSynthetic != rightLocal.IsSynthetic ||
                !AreEquivalent(
                    leftLocal.DependencyExpression,
                    rightLocal.DependencyExpression) ||
                !StringComparer.Ordinal.Equals(
                    leftLocal.DeclaredValueKey,
                    rightLocal.DeclaredValueKey) ||
                !StringComparer.Ordinal.Equals(
                    leftLocal.StoredValueTypeName,
                    rightLocal.StoredValueTypeName))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalent(
        TypeMapperDependencyExpressionModel? left,
        TypeMapperDependencyExpressionModel? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left is not null &&
               right is not null &&
               AreEquivalent(left.Root, right.Root);
    }

    private static bool AreEquivalent(
        TypeMapperDependencyExpressionNodeModel left,
        TypeMapperDependencyExpressionNodeModel right)
    {
        var childCount = Count(left.Children);

        if (!StringComparer.Ordinal.Equals(left.Key, right.Key) ||
            !StringComparer.Ordinal.Equals(
                left.ValueTypeName,
                right.ValueTypeName) ||
            left.CanMaterialize != right.CanMaterialize ||
            !StringComparer.Ordinal.Equals(
                left.ExpressionTemplate,
                right.ExpressionTemplate) ||
            childCount != Count(right.Children))
        {
            return false;
        }

        for (var index = 0; index < childCount; index++)
        {
            var leftChild = left.Children[index];
            var rightChild = right.Children[index];

            if (!StringComparer.Ordinal.Equals(
                    leftChild.Placeholder,
                    rightChild.Placeholder) ||
                !AreEquivalent(leftChild.Node, rightChild.Node))
            {
                return false;
            }
        }

        return true;
    }

    private static int Count<T>(ImmutableArray<T> values)
    {
        return values.IsDefault
            ? 0
            : values.Length;
    }
}
