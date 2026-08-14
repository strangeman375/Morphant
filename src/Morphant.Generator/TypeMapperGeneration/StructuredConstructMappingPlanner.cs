using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.ConstructionSurface.ConstructionPlan;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class StructuredConstructMappingPlanner
{
    private const string UnsupportedConstructMessage =
        "This Construct or Resolve expression is not supported.";

    private const string UnavailablePreviousMessage =
        "'previous' is not available in this case.";

    private const string ByConventionMarkerMetadataName =
        "Morphant.Markers.ByConventionMarker";

    public static StructuredConstructMappingResult Build(
        ResultPolicyConfigurationModel configuration,
        TypeMapperMappingModel mapping,
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        MappingPairCapabilities capabilities,
        ConventionMemberMappingPlan memberMappings,
        ConstructorSelectionValue? constructorSelection,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        HashSet<string> usedGeneratedMethodNames,
        CancellationToken cancellationToken)
    {
        if (configuration.Expression.Syntax is not
                LambdaExpressionSyntax lambda ||
            TryGetLambdaParameters(
                lambda,
                configuration.Expression.SemanticModel,
                configuration.Form,
                cancellationToken,
                out var sourceParameter,
                out var previousParameter,
                out var contextParameter) is false ||
            !DeclarativeContextUsagePolicy.IsSupported(
                lambda,
                contextParameter,
                configuration.Expression.SemanticModel,
                cancellationToken) ||
            !DeclarativeDeferredCapturePolicy.IsSupported(
                lambda,
                previousParameter,
                resultParameter: null,
                contextParameter,
                configuration.Expression.SemanticModel,
                cancellationToken))
        {
            return StructuredConstructMappingResult.Unsupported(
                BuildFailure(
                    mapping,
                    configuration,
                    MappingFailureReason.UnsupportedStructuredCallback,
                    UnsupportedConstructMessage));
        }

        var transferScope = (SyntaxNode?)lambda.ExpressionBody ??
                            lambda.Block;

        if (transferScope is null)
        {
            return StructuredConstructMappingResult.Unsupported(
                BuildFailure(
                    mapping,
                    configuration,
                    MappingFailureReason.UnsupportedStructuredCallback,
                    UnsupportedConstructMessage));
        }

        if (DeclarativeControlFlowPlanner.Build(
                lambda,
                configuration.Expression.SemanticModel,
                cancellationToken) is not
            DeclarativeControlFlowProgram controlFlowProgram)
        {
            return StructuredConstructMappingResult.Unsupported(
                BuildFailure(
                    mapping,
                    configuration,
                    MappingFailureReason.UnsupportedStructuredCallback,
                    UnsupportedConstructMessage));
        }

        TypeMapperControlFlowNode? BuildPlan(
            bool? previousAvailable)
        {
            var nestedMapUsages =
                new DeclarativeNestedMapUsageRegistry(
                    previousAvailable == true
                        ? MappingExecutionPathSet.UpdateWithPrevious
                        : MappingExecutionPathSet.NoPrevious);
            var replacement = previousAvailable == true;
            var constructorMembers =
                memberMappings.BuildConstructorInitializationPlan(replacement);
            PreviousExpressionSubstitution? previousSubstitution =
                previousParameter is not null &&
                previousAvailable is { } hasPrevious
                    ? BuildPreviousSubstitution(
                        mapping,
                        hasPrevious)
                    : null;

            string? Rewrite(ExpressionSyntax expression)
            {
                return ConstructExpressionRewriter.TryRewriteWithContext(
                        expression,
                        configuration.Expression.SemanticModel,
                        mapperType,
                        sourceParameter,
                        mapping.NonNullSourceName,
                        previousParameter,
                        previousSubstitution,
                        resultParameter: null,
                        resultName: null,
                        contextParameter,
                        contextName: "context",
                        transferScope,
                        controlFlowProgram.RuntimeLocalPlaceholders,
                        cancellationToken,
                        out var rewritten)
                    ? rewritten
                    : null;
            }

            TypeMapperRewrittenDependencyExpression?
                RewriteDependencyCore(
                    ExpressionSyntax expression,
                    ITypeSymbol? fallbackType,
                    DeclarativeNestedMapTargetContext? nestedMapTarget = null)
            {
                return DeclarativeDependencyExpressionBuilder
                    .TryRewriteWithContext(
                        expression,
                        configuration.Expression.SemanticModel,
                        mapperType,
                        sourceParameter,
                        mapping.NonNullSourceName,
                        previousParameter,
                        previousSubstitution,
                        resultParameter: null,
                        resultName: null,
                        contextParameter,
                        contextName: "context",
                        transferScope,
                        controlFlowProgram.RuntimeLocalPlaceholders,
                        fallbackType,
                        nestedMapTarget,
                        nestedMapUsages,
                        cancellationToken,
                        out var rewritten,
                        out var dependency)
                    ? new TypeMapperRewrittenDependencyExpression(
                        rewritten,
                        dependency)
                    : null;
            }

            TypeMapperRewrittenDependencyExpression?
                RewriteDependency(
                    ExpressionSyntax expression,
                    IParameterSymbol parameter)
            {
                var parameterType = DeclarativeIntrinsic
                        .TryGetWrapperTargetType(
                            expression,
                            MetadataNames.ConstructorParameter,
                            configuration.Expression.SemanticModel,
                            cancellationToken,
                            out var contextualTargetType)
                    ? contextualTargetType
                    : parameter.Type.WithNullableAnnotation(
                        parameter.NullableAnnotation);
                var destinationMembers =
                    ConventionMemberMappingPlanner.BuildReadableMembers(
                        destination,
                        compilation,
                        mapperType,
                        cancellationToken);
                var destinationMember =
                    ConventionConstructorMappingPlanner
                        .TryFindSourceMember(
                            destinationMembers,
                            parameter.Name);
                var targetName = destinationMember?.Name ??
                    parameter.Name;
                var currentDestination =
                    previousAvailable == true &&
                    destinationMember is { } currentMember
                        ? "destination." +
                          Identifier(currentMember.Name)
                        : null;

                return RewriteDependencyCore(
                    expression,
                    parameterType,
                    new DeclarativeNestedMapTargetContext(
                        parameterType,
                        targetName,
                        previousAvailable == true
                            ? DeclarativeNestedMapOperation.Update
                            : DeclarativeNestedMapOperation.Create,
                        currentDestination,
                        destinationMember?.Type,
                        parameter,
                        TargetDesignator: null,
                        destinationMember?.Symbol,
                        previousAvailable == true
                            ? MappingExecutionPathSet.UpdateWithPrevious
                            : MappingExecutionPathSet.NoPrevious));
            }

            StructuredConstructPlanNode? BuildExpression(
                ExpressionSyntax expression,
                ImmutableArray<DeclarativeTerminalAliasSyntax> aliases) =>
                BuildPlanNode(
                    expression,
                    sourceType,
                    destination,
                    capabilities,
                    constructorMembers,
                    constructorSelection,
                    compilation,
                    mapperType,
                    configuration.Expression.SemanticModel,
                    mapping.NonNullSourceName,
                    Rewrite,
                    RewriteDependency,
                    (_, whenTrue, whenFalse) =>
                        AreEquivalentPlanNodes(whenTrue, whenFalse)
                            ? whenTrue
                            : null,
                    previousParameter,
                    mapping,
                    configuration.Expression.DeclaringMapperType,
                    previousAvailable == true
                        ? MappingExecutionPathSet.UpdateWithPrevious
                        : MappingExecutionPathSet.NoPrevious,
                    aliases,
                    cancellationToken);

            TypeMapperControlFlowNode? BuildLeaf(
                DeclarativeLeafSyntaxNode leaf)
            {
                StructuredConstructPlanNode? plannedLeaf;

                if (leaf.DirectExpression is
                        { } directExpression)
                {
                    plannedLeaf = leaf.ObjectCreation is null &&
                                  leaf.Arguments.IsEmpty &&
                                  leaf.MemberAssignments.IsEmpty
                        ? BuildExpression(
                            directExpression,
                            leaf.TerminalAliases)
                        : null;
                }
                else if (leaf.ObjectCreation is not null &&
                         leaf.MemberAssignments.IsEmpty)
                {
                    var arguments = leaf.Arguments.Select(argument =>
                            new StructuredObjectArgument(
                                argument.Syntax,
                                argument.Value,
                                argument.MemberAssignments))
                        .ToImmutableArray();

                    if (ContainsMarker(
                            arguments,
                            ByConventionMarkerMetadataName,
                            configuration.Expression.SemanticModel,
                            cancellationToken))
                    {
                        var convention = BuildByConventionPlan(
                            arguments,
                            sourceType,
                            destination,
                            capabilities,
                            constructorMembers,
                            constructorSelection,
                            compilation,
                            mapperType,
                            configuration.Expression.SemanticModel,
                            mapping.NonNullSourceName,
                            Rewrite,
                            RewriteDependency,
                            cancellationToken);

                        plannedLeaf = convention.Plan is not { } conventionPlan
                            ? BuildUnsupportedPlanLeaf(
                                mapping,
                                configuration.Expression.DeclaringMapperType,
                                leaf.ObjectCreation,
                                previousAvailable == true
                                    ? MappingExecutionPathSet
                                        .UpdateWithPrevious
                                    : MappingExecutionPathSet.NoPrevious,
                                MappingFailureReason
                                    .ConstructorSelectionFailed,
                                convention.Observation)
                            : new StructuredConstructLeafNode(
                                StructuredConstructLeafKind.Constructor,
                                conventionPlan,
                                convention.Observation,
                                Failure: null,
                                Terminal: null);
                    }
                    else
                    {
                        var explicitPlanning =
                            ExplicitStructuredConstructorPlanner.Build(
                                arguments,
                                sourceType,
                                destination,
                                compilation,
                                mapperType,
                                configuration.Expression.SemanticModel,
                                Rewrite,
                                RewriteDependency,
                                leaf.ObjectCreation,
                                cancellationToken);

                        if (explicitPlanning.Plan is not { } explicitPlan)
                        {
                            plannedLeaf = BuildUnsupportedPlanLeaf(
                                mapping,
                                configuration.Expression.DeclaringMapperType,
                                leaf.ObjectCreation,
                                previousAvailable == true
                                    ? MappingExecutionPathSet
                                        .UpdateWithPrevious
                                    : MappingExecutionPathSet.NoPrevious,
                                MappingFailureReason
                                    .ConstructorParameterRuleInvalid,
                                explicitPlanning.Observation);
                        }
                        else if (ConventionConstructorMappingPlanner
                                     .BuildExplicitPlan(
                                         destination,
                                         constructorMembers,
                                         explicitPlan.Constructor,
                                         explicitPlan.Arguments,
                                         compilation,
                                         mapperType,
                                         mapping.NonNullSourceName,
                                         cancellationToken) is
                                 { } constructor)
                        {
                            constructor = constructor with
                            {
                                Observation = explicitPlanning.Observation
                            };
                            plannedLeaf = new StructuredConstructLeafNode(
                                StructuredConstructLeafKind.Constructor,
                                constructor,
                                explicitPlanning.Observation,
                                Failure: null,
                                Terminal: null);
                        }
                        else
                        {
                            var observation =
                                ObserveMemberConstraintFailure(
                                    explicitPlanning.Observation,
                                    constructorMembers);
                            plannedLeaf = BuildUnsupportedPlanLeaf(
                                mapping,
                                configuration.Expression.DeclaringMapperType,
                                leaf.ObjectCreation,
                                previousAvailable == true
                                    ? MappingExecutionPathSet
                                        .UpdateWithPrevious
                                    : MappingExecutionPathSet.NoPrevious,
                                MappingFailureReason
                                    .ConstructorParameterRuleInvalid,
                                observation);
                        }
                    }
                }
                else
                {
                    plannedLeaf = null;
                }

                if (plannedLeaf is null)
                {
                    return null;
                }

                return BuildRuntimeNode(
                    plannedLeaf,
                    mapping,
                    memberMappings,
                    nestedMapUsages.Observations,
                    create: previousAvailable != true);
            }

            return DeclarativeControlFlowLowerer.TryBuild(
                    controlFlowProgram,
                    configuration.Expression.SemanticModel,
                    compilation,
                    mapperType,
                    sourceParameter,
                    mapping.NonNullSourceName,
                    previousParameter,
                    previousSubstitution,
                    resultParameter: null,
                    resultName: null,
                    contextParameter,
                    contextName: "context",
                    transferScope,
                    mapping,
                    BuildLeaf,
                    (condition, whenTrue, whenFalse) =>
                        BuildRuntimeConditionNode(
                            condition,
                            whenTrue,
                            whenFalse,
                            expression =>
                                RewriteDependencyCore(
                                    expression,
                                    fallbackType: null,
                                    nestedMapTarget: null),
                            previousParameter,
                            previousAvailable,
                            configuration.Expression.SemanticModel,
                            cancellationToken),
                    previousAvailable == true
                        ? MappingExecutionPathSet.UpdateWithPrevious
                        : MappingExecutionPathSet.NoPrevious,
                    cancellationToken,
                    out var lowered)
                ? DeclarativeControlFlowLowerer.PreserveLocalNames(
                    lowered)
                : null;
        }

        TypeMapperControlFlowNode createRoot;
        TypeMapperControlFlowNode updateRoot;

        if (configuration.Kind == ResultPolicyKind.Construct)
        {
            var plannedRoot = BuildPlan(previousAvailable: null);

            if (plannedRoot is null)
            {
                return StructuredConstructMappingResult.Unsupported(
                    BuildFailure(
                        mapping,
                        configuration,
                        MappingFailureReason.UnsupportedStructuredCallback,
                        UnsupportedConstructMessage));
            }

            createRoot = plannedRoot;
            updateRoot = BuildPreviousLeaf(
                mapping,
                memberMappings,
                create: false);
        }
        else
        {
            var createPlan = BuildPlan(previousAvailable: false);
            var updatePlan = BuildPlan(previousAvailable: true);

            if (createPlan is null || updatePlan is null)
            {
                return StructuredConstructMappingResult.Unsupported(
                    BuildFailure(
                        mapping,
                        configuration,
                        MappingFailureReason.UnsupportedStructuredCallback,
                        UnsupportedConstructMessage));
            }

            createRoot = createPlan;
            updateRoot = updatePlan;
        }

        return new StructuredConstructMappingResult(
            new TypeMapperControlFlowMappingModel(
                createRoot,
                updateRoot),
            HelperMethodDeclarations: ImmutableArray<string>.Empty,
            Failure: null);
    }

    private static PreviousExpressionSubstitution
        BuildPreviousSubstitution(
            TypeMapperMappingModel mapping,
            bool hasPrevious)
    {
        var optionTypeName =
            "global::Morphant.Option<" +
            mapping.NonNullDestinationTypeName +
            ">";

        return hasPrevious
            ? new PreviousExpressionSubstitution(
                optionTypeName + ".Some(destination)",
                "destination",
                HasValueExpression: "true")
            : new PreviousExpressionSubstitution(
                optionTypeName + ".None",
                optionTypeName + ".None.Value",
                HasValueExpression: "false");
    }

    private static TypeMapperControlFlowNode?
        BuildRuntimeConditionNode(
            ExpressionSyntax condition,
            TypeMapperControlFlowNode whenTrue,
            TypeMapperControlFlowNode whenFalse,
            Func<ExpressionSyntax,
                TypeMapperRewrittenDependencyExpression?>
                rewriteExpression,
            IParameterSymbol? previousParameter,
            bool? previousAvailable,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
    {
        condition = UnwrapParentheses(condition);

        if (TryEvaluateKnownCondition(
                condition,
                previousParameter,
                previousAvailable,
                semanticModel,
                cancellationToken,
                out var knownValue))
        {
            return knownValue
                ? whenTrue
                : whenFalse;
        }

        if (previousParameter is not null &&
            previousAvailable is not null &&
            ReferencesPreviousAvailability(
                condition,
                previousParameter,
                semanticModel,
                cancellationToken))
        {
            if (condition is PrefixUnaryExpressionSyntax
                {
                    RawKind:
                        (int)SyntaxKind.LogicalNotExpression,
                    Operand: var operand
                })
            {
                return BuildRuntimeConditionNode(
                    operand,
                    whenFalse,
                    whenTrue,
                    rewriteExpression,
                    previousParameter,
                    previousAvailable,
                    semanticModel,
                    cancellationToken);
            }

            if (condition is BinaryExpressionSyntax binary)
            {
                switch ((SyntaxKind)binary.RawKind)
                {
                    case SyntaxKind.LogicalAndExpression:
                    {
                        var whenLeftTrue =
                            BuildRuntimeConditionNode(
                                binary.Right,
                                whenTrue,
                                whenFalse,
                                rewriteExpression,
                                previousParameter,
                                previousAvailable,
                                semanticModel,
                                cancellationToken);

                        return whenLeftTrue is null
                            ? null
                            : BuildRuntimeConditionNode(
                                binary.Left,
                                whenLeftTrue,
                                whenFalse,
                                rewriteExpression,
                                previousParameter,
                                previousAvailable,
                                semanticModel,
                                cancellationToken);
                    }

                    case SyntaxKind.LogicalOrExpression:
                    {
                        var whenLeftFalse =
                            BuildRuntimeConditionNode(
                                binary.Right,
                                whenTrue,
                                whenFalse,
                                rewriteExpression,
                                previousParameter,
                                previousAvailable,
                                semanticModel,
                                cancellationToken);

                        return whenLeftFalse is null
                            ? null
                            : BuildRuntimeConditionNode(
                                binary.Left,
                                whenTrue,
                                whenLeftFalse,
                                rewriteExpression,
                                previousParameter,
                                previousAvailable,
                                semanticModel,
                                cancellationToken);
                    }
                }
            }
        }

        var rewrittenCondition = rewriteExpression(condition);

        if (rewrittenCondition is null)
        {
            return null;
        }

        return TypeMapperRuntimeEquality.AreEquivalent(
            whenTrue,
            whenFalse)
            ? new TypeMapperControlFlowNode(
                Locals: ImmutableArray<TypeMapperLocalValueModel>.Empty,
                Condition: null,
                WhenTrue: null,
                WhenFalse: null,
                Leaf: null,
                ThrowExpression: null,
                EvaluationExpression:
                    rewrittenCondition.Value.Expression,
                EvaluationContinuation: whenTrue,
                EvaluationDependency:
                    rewrittenCondition.Value.DependencyExpression)
            : new TypeMapperControlFlowNode(
                Locals: ImmutableArray<TypeMapperLocalValueModel>.Empty,
                rewrittenCondition.Value.Expression,
                whenTrue,
                whenFalse,
                Leaf: null,
                ThrowExpression: null,
                ConditionDependency:
                    rewrittenCondition.Value.DependencyExpression);
    }

    private static bool TryEvaluateKnownCondition(
        ExpressionSyntax condition,
        IParameterSymbol? previousParameter,
        bool? previousAvailable,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out bool value)
    {
        condition = UnwrapParentheses(condition);

        if (semanticModel.GetConstantValue(
                condition,
                cancellationToken) is
            {
                HasValue: true,
                Value: bool constant
            })
        {
            value = constant;
            return true;
        }

        if (previousParameter is not null &&
            previousAvailable is { } hasPrevious &&
            IsPreviousAvailabilityAccess(
                condition,
                previousParameter,
                semanticModel,
                cancellationToken))
        {
            value = hasPrevious;
            return true;
        }

        if (condition is PrefixUnaryExpressionSyntax
            {
                RawKind:
                    (int)SyntaxKind.LogicalNotExpression,
                Operand: var operand
            } &&
            TryEvaluateKnownCondition(
                operand,
                previousParameter,
                previousAvailable,
                semanticModel,
                cancellationToken,
                out var operandValue))
        {
            value = !operandValue;
            return true;
        }

        if (condition is BinaryExpressionSyntax binary)
        {
            if (TryEvaluateKnownCondition(
                    binary.Left,
                    previousParameter,
                    previousAvailable,
                    semanticModel,
                    cancellationToken,
                    out var leftValue))
            {
                switch ((SyntaxKind)binary.RawKind)
                {
                    case SyntaxKind.LogicalAndExpression
                        when !leftValue:
                        value = false;
                        return true;

                    case SyntaxKind.LogicalOrExpression
                        when leftValue:
                        value = true;
                        return true;
                }

                if (TryEvaluateKnownCondition(
                        binary.Right,
                        previousParameter,
                        previousAvailable,
                        semanticModel,
                        cancellationToken,
                        out var rightValue))
                {
                    switch ((SyntaxKind)binary.RawKind)
                    {
                        case SyntaxKind.LogicalAndExpression:
                            value = leftValue && rightValue;
                            return true;

                        case SyntaxKind.LogicalOrExpression:
                            value = leftValue || rightValue;
                            return true;

                        case SyntaxKind.EqualsExpression:
                            value = leftValue == rightValue;
                            return true;

                        case SyntaxKind.NotEqualsExpression:
                            value = leftValue != rightValue;
                            return true;
                    }
                }
            }
        }

        value = false;
        return false;
    }

    private static bool ReferencesPreviousAvailability(
        ExpressionSyntax condition,
        IParameterSymbol previousParameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return condition.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(access =>
                IsPreviousAvailabilityAccess(
                    access,
                    previousParameter,
                    semanticModel,
                    cancellationToken));
    }

    private static bool IsPreviousAvailabilityAccess(
        ExpressionSyntax expression,
        IParameterSymbol previousParameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        expression = UnwrapParentheses(expression);

        return expression is MemberAccessExpressionSyntax
               {
                   Expression: var receiver,
                   Name.Identifier.ValueText: "HasValue"
               } &&
               IsParameterReference(
                   receiver,
                   previousParameter,
                   semanticModel,
                   cancellationToken);
    }

    private static StructuredConstructPlanNode? BuildPlanNode(
        ExpressionSyntax expression,
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        MappingPairCapabilities capabilities,
        ConstructorInitializationMappingPlan memberMappings,
        ConstructorSelectionValue? constructorSelection,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        SemanticModel semanticModel,
        string nonNullSourceName,
        Func<ExpressionSyntax, string?> rewriteExpression,
        Func<ExpressionSyntax, IParameterSymbol,
            TypeMapperRewrittenDependencyExpression?>
            rewriteDependencyExpression,
        Func<
            ExpressionSyntax,
            StructuredConstructPlanNode,
            StructuredConstructPlanNode,
            StructuredConstructPlanNode?> buildCondition,
        IParameterSymbol? previousParameter,
        TypeMapperMappingModel mapping,
        INamedTypeSymbol sourceMapper,
        MappingExecutionPathSet paths,
        ImmutableArray<DeclarativeTerminalAliasSyntax> aliases,
        CancellationToken cancellationToken)
    {
        expression = UnwrapParentheses(expression);

        if (expression is ConditionalExpressionSyntax conditional)
        {
            var whenTrue = BuildPlanNode(
                conditional.WhenTrue,
                sourceType,
                destination,
                capabilities,
                memberMappings,
                constructorSelection,
                compilation,
                mapperType,
                semanticModel,
                nonNullSourceName,
                rewriteExpression,
                rewriteDependencyExpression,
                buildCondition,
                previousParameter,
                mapping,
                sourceMapper,
                paths,
                aliases,
                cancellationToken);
            var whenFalse = BuildPlanNode(
                conditional.WhenFalse,
                sourceType,
                destination,
                capabilities,
                memberMappings,
                constructorSelection,
                compilation,
                mapperType,
                semanticModel,
                nonNullSourceName,
                rewriteExpression,
                rewriteDependencyExpression,
                buildCondition,
                previousParameter,
                mapping,
                sourceMapper,
                paths,
                aliases,
                cancellationToken);

            return whenTrue is null || whenFalse is null
                ? null
                : buildCondition(
                    conditional.Condition,
                    whenTrue,
                    whenFalse);
        }

        if (previousParameter is not null &&
            IsParameterReference(
                expression,
                previousParameter,
                semanticModel,
                cancellationToken))
        {
            return new StructuredConstructLeafNode(
                StructuredConstructLeafKind.Previous,
                Constructor: null,
                ConstructorObservation: null,
                Failure: null,
                Terminal: new StructuredTerminalObservation(
                    StructuredTerminalKind.Previous,
                    expression,
                    new MappingAffectedPath(
                        paths,
                        MappingPlanPhase.Construction,
                        expression),
                    aliases));
        }

        if (TryGetOmittedProducer(expression, out var omittedProducer))
        {
            return BuildUnsupportedPlanLeaf(
                mapping,
                sourceMapper,
                omittedProducer,
                paths,
                MappingFailureReason.TerminalNullConstruction,
                terminalAliases: aliases);
        }

        if (expression is not BaseObjectCreationExpressionSyntax creation ||
            creation.Initializer is not null)
        {
            return BuildUnsupportedPlanLeaf(
                mapping,
                sourceMapper,
                expression,
                paths,
                MappingFailureReason.UnsupportedStructuredSyntax);
        }

        var arguments = BuildObjectArguments(creation);

        if (ContainsMarker(
                arguments,
                ByConventionMarkerMetadataName,
                semanticModel,
                cancellationToken))
        {
            var convention = BuildByConventionPlan(
                arguments,
                sourceType,
                destination,
                capabilities,
                memberMappings,
                constructorSelection,
                compilation,
                mapperType,
                semanticModel,
                nonNullSourceName,
                rewriteExpression,
                rewriteDependencyExpression,
                cancellationToken);

            return convention.Plan is not { } conventionPlan
                ? BuildUnsupportedPlanLeaf(
                    mapping,
                    sourceMapper,
                    creation,
                    paths,
                    MappingFailureReason.ConstructorSelectionFailed,
                    convention.Observation)
                : new StructuredConstructLeafNode(
                    StructuredConstructLeafKind.Constructor,
                    conventionPlan,
                    convention.Observation,
                    Failure: null,
                    Terminal: null);
        }

        var explicitPlanning =
            ExplicitStructuredConstructorPlanner.Build(
                arguments,
                sourceType,
                destination,
                compilation,
                mapperType,
                semanticModel,
                rewriteExpression,
                rewriteDependencyExpression,
                creation,
                cancellationToken);

        if (explicitPlanning.Plan is not { } explicitPlan)
        {
            return BuildUnsupportedPlanLeaf(
                mapping,
                sourceMapper,
                creation,
                paths,
                MappingFailureReason.ConstructorParameterRuleInvalid,
                explicitPlanning.Observation);
        }

        var constructor =
            ConventionConstructorMappingPlanner.BuildExplicitPlan(
                destination,
                memberMappings,
                explicitPlan.Constructor,
                explicitPlan.Arguments,
                compilation,
                mapperType,
                nonNullSourceName,
                cancellationToken);

        if (constructor is not { } resolvedConstructor)
        {
            return BuildUnsupportedPlanLeaf(
                mapping,
                sourceMapper,
                creation,
                paths,
                MappingFailureReason.ConstructorParameterRuleInvalid,
                ObserveMemberConstraintFailure(
                    explicitPlanning.Observation,
                    memberMappings));
        }

        resolvedConstructor = resolvedConstructor with
        {
            Observation = explicitPlanning.Observation
        };

        return new StructuredConstructLeafNode(
            StructuredConstructLeafKind.Constructor,
            resolvedConstructor,
            explicitPlanning.Observation,
            Failure: null,
            Terminal: null);
    }

    private static ConventionConstructorPlanningResult
        BuildByConventionPlan(
            ImmutableArray<StructuredObjectArgument> arguments,
            ITypeSymbol sourceType,
            INamedTypeSymbol destination,
            MappingPairCapabilities capabilities,
            ConstructorInitializationMappingPlan memberMappings,
            ConstructorSelectionValue? constructorSelection,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            SemanticModel semanticModel,
            string nonNullSourceName,
            Func<ExpressionSyntax, string?> rewriteExpression,
            Func<ExpressionSyntax, IParameterSymbol,
                TypeMapperRewrittenDependencyExpression?>
                rewriteDependencyExpression,
            CancellationToken cancellationToken)
    {
        var constructors =
            DestinationCapabilityPolicy.GetSupportedConstructors(
                destination,
                compilation,
                cancellationToken);
        var strategyOrigin = arguments.FirstOrDefault(argument =>
                IsMarker(
                    argument.Value,
                    ByConventionMarkerMetadataName,
                    semanticModel,
                    cancellationToken))
            .Value;

        ConventionConstructorPlanningResult ObserveStrategy(
            ConventionConstructorPlanningResult planning)
        {
            var observation = planning.Observation with
            {
                Strategy = constructorSelection,
                StrategyOrigin = strategyOrigin
            };

            return new ConventionConstructorPlanningResult(
                planning.Plan is { } plan
                    ? plan with
                    {
                        Observation = observation
                    }
                    : null,
                observation);
        }

        ConventionConstructorPlanningResult Unsupported(
            ConstructorCandidateRejectionReason rejection) =>
            ObserveStrategy(new ConventionConstructorPlanningResult(
                Plan: null,
                new ConstructorPlanningObservation(
                    constructorSelection,
                    strategyOrigin,
                    constructors.Select(constructor =>
                            new ConstructorCandidateObservation(
                                constructor,
                                ParameterRules: ImmutableArray<ConstructorParameterRuleObservation>.Empty,
                                rejection))
                        .ToImmutableArray(),
                    SelectedConstructor: null,
                    Terminals: ImmutableArray<StructuredTerminalObservation>.Empty)));

        if (constructorSelection is null)
        {
            return Unsupported(
                ConstructorCandidateRejectionReason.StrategyShape);
        }

        if (!TryGetByConventionRules(
                arguments,
                destination,
                compilation,
                semanticModel,
                cancellationToken,
                out var rules))
        {
            return Unsupported(
                ConstructorCandidateRejectionReason.ExplicitRule);
        }

        if (rules.IsEmpty)
        {
            return ObserveStrategy(
                ConventionConstructorMappingPlanner.Build(
                    sourceType,
                    destination,
                    memberMappings,
                    capabilities,
                    constructorSelection,
                    compilation,
                    mapperType,
                    nonNullSourceName,
                    cancellationToken));
        }

        var sourceMembers =
            ConventionMemberMappingPlanner.BuildReadableMembers(
                sourceType,
                compilation,
                mapperType,
                cancellationToken);
        var destinationMembers =
            ConventionConstructorMappingPlanner
                .BuildConstructorDestinationMembers(
                    destination,
                    memberMappings.Observation,
                    compilation,
                    mapperType,
                    cancellationToken);
        var plannedCandidates = constructors.Select(constructor =>
                BuildByConventionPlanForConstructor(
                    constructor,
                    sourceType,
                    rules,
                    sourceMembers,
                    destinationMembers,
                    destination,
                    memberMappings,
                    compilation,
                    mapperType,
                    semanticModel,
                    nonNullSourceName,
                    rewriteExpression,
                    rewriteDependencyExpression,
                    cancellationToken))
            .ToImmutableArray();
        ConventionConstructorMappingPlan? selectedPlan = null;
        IMethodSymbol? selectedConstructor = null;

        if (constructorSelection ==
            ConstructorSelectionValue.Greediest)
        {
            var selectedArgumentCount = -1;
            var hasTie = false;

            foreach (var candidate in plannedCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (candidate.Plan is not { } candidatePlan)
                {
                    continue;
                }

                var argumentCount =
                    candidatePlan.Constructor.Arguments.Length;

                if (argumentCount > selectedArgumentCount)
                {
                    selectedPlan = candidatePlan;
                    selectedConstructor = candidate.Constructor;
                    selectedArgumentCount = argumentCount;
                    hasTie = false;
                }
                else if (argumentCount == selectedArgumentCount)
                {
                    hasTie = true;
                }
            }

            if (hasTie)
            {
                selectedPlan = null;
                selectedConstructor = null;
            }
        }
        else if (ConventionConstructorMappingPlanner.TrySelectConstructor(
                     constructors,
                     constructorSelection.Value) is { } constructor)
        {
            selectedConstructor = constructor;
            selectedPlan = plannedCandidates.First(candidate =>
                    SymbolEqualityComparer.Default.Equals(
                        candidate.Constructor,
                        constructor))
                .Plan;
        }

        var observation = new ConstructorPlanningObservation(
            constructorSelection,
            strategyOrigin,
            plannedCandidates.Select(static candidate =>
                    candidate.Observation)
                .ToImmutableArray(),
            selectedConstructor,
            Terminals: ImmutableArray<StructuredTerminalObservation>.Empty);

        return ObserveStrategy(new ConventionConstructorPlanningResult(
            selectedPlan,
            observation));
    }

    private static StructuredConstructorCandidatePlanningResult
        BuildByConventionPlanForConstructor(
            IMethodSymbol constructor,
            ITypeSymbol sourceType,
            ImmutableArray<StructuredConstructorParameterRule> rules,
            ImmutableArray<ConventionReadableMember> sourceMembers,
            ImmutableArray<ISymbol> destinationMembers,
            INamedTypeSymbol destination,
            ConstructorInitializationMappingPlan memberMappings,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            SemanticModel semanticModel,
            string nonNullSourceName,
            Func<ExpressionSyntax, string?> rewriteExpression,
            Func<ExpressionSyntax, IParameterSymbol,
                TypeMapperRewrittenDependencyExpression?>
                rewriteDependencyExpression,
            CancellationToken cancellationToken)
    {
        var configuredParameterNames =
            new HashSet<string>(StringComparer.Ordinal);
        var mappedArguments =
            ImmutableArray.CreateBuilder<
                TypeMapperConstructorArgumentMappingModel>();
        var parameterObservations =
            ImmutableArray.CreateBuilder<
                ConstructorParameterRuleObservation>();
        var rejection = ConstructorCandidateRejectionReason.None;

        void Reject(ConstructorCandidateRejectionReason reason)
        {
            if (rejection == ConstructorCandidateRejectionReason.None)
            {
                rejection = reason;
            }
        }

        foreach (var rule in rules)
        {
            var parameter = constructor.Parameters.FirstOrDefault(
                candidate => StringComparer.Ordinal.Equals(
                    candidate.Name,
                    rule.ParameterName));

            if (parameter is null)
            {
                Reject(ConstructorCandidateRejectionReason.ExplicitRule);
                parameterObservations.Add(
                    new ConstructorParameterRuleObservation(
                        Parameter: null,
                        rule.ParameterName,
                        ConstructorParameterRuleOrigin.Value,
                        rule.Value,
                        SourceMember: null,
                        DestinationMember: null,
                        IsApplicable: false,
                        ConstructorCandidateRejectionReason.ExplicitRule,
                        rule.DesignatorNode));
                continue;
            }

            var destinationMember =
                ConventionConstructorMappingPlanner
                    .FindAssociatedDestinationMember(
                        destinationMembers,
                        parameter.Name);

            if (!configuredParameterNames.Add(parameter.Name))
            {
                Reject(ConstructorCandidateRejectionReason.ExplicitRule);
                parameterObservations.Add(
                    new ConstructorParameterRuleObservation(
                        parameter,
                        parameter.Name,
                        ConstructorParameterRuleOrigin.Value,
                        rule.Value,
                        SourceMember: null,
                        destinationMember,
                        IsApplicable: false,
                        ConstructorCandidateRejectionReason.ExplicitRule,
                        rule.DesignatorNode));
                continue;
            }

            if (DeclarativeConstructorMarker.TryGetKind(
                    rule.Value,
                    DeclarativeIntrinsic.TryGetWrapperTargetType(
                        rule.Value,
                        MetadataNames.ConstructorParameter,
                        semanticModel,
                        cancellationToken,
                        out var contextualTargetType)
                        ? contextualTargetType
                        : parameter.Type.WithNullableAnnotation(
                            parameter.NullableAnnotation),
                    semanticModel,
                    mapperType,
                    cancellationToken,
                    out var markerKind))
            {
                if (markerKind ==
                    DeclarativeConstructorMarkerKind.Ignore)
                {
                    var canOmit =
                        ConventionConstructorMappingPlanner.CanOmit(
                            parameter);
                    parameterObservations.Add(
                        new ConstructorParameterRuleObservation(
                            parameter,
                            parameter.Name,
                            ConstructorParameterRuleOrigin.Ignore,
                            rule.Value,
                            SourceMember: null,
                            destinationMember,
                            canOmit,
                            canOmit
                                ? ConstructorCandidateRejectionReason.None
                                : ConstructorCandidateRejectionReason
                                    .ExplicitRule,
                            rule.DesignatorNode));

                    if (!canOmit)
                    {
                        Reject(
                            ConstructorCandidateRejectionReason.ExplicitRule);
                    }

                    continue;
                }

                if (markerKind ==
                    DeclarativeConstructorMarkerKind.Auto)
                {
                    var sourceMember =
                        ConventionConstructorMappingPlanner
                            .TryFindSourceMember(
                                sourceMembers,
                                parameter.Name);
                    var compatible = sourceMember is { } candidate &&
                        MappingExpressionCompatibility
                            .HasPotentiallyCompatibleConversion(
                                candidate.Type,
                                parameter.Type,
                                compilation);
                    var ruleRejection = sourceMember is null
                        ? ConstructorCandidateRejectionReason
                            .MissingSourceMember
                        : compatible
                            ? ConstructorCandidateRejectionReason.None
                            : ConstructorCandidateRejectionReason
                                .IncompatibleArgument;
                    parameterObservations.Add(
                        new ConstructorParameterRuleObservation(
                            parameter,
                            parameter.Name,
                            ConstructorParameterRuleOrigin.Auto,
                            rule.Value,
                            sourceMember?.Symbol,
                            destinationMember,
                            compatible,
                            ruleRejection,
                            rule.DesignatorNode));

                    if (!compatible || sourceMember is null)
                    {
                        Reject(ruleRejection);
                        continue;
                    }

                    mappedArguments.Add(
                        BuildAutomaticArgument(
                            sourceMember.Value,
                            parameter,
                            rule.Value,
                            ConstructorParameterRuleOrigin.Auto));
                    continue;
                }
            }

            var rewrittenDependency =
                rewriteDependencyExpression(
                    rule.Value,
                    parameter);
            var explicitExpression =
                rewrittenDependency?.Expression;

            if (explicitExpression is null)
            {
                Reject(ConstructorCandidateRejectionReason.ExplicitRule);
                parameterObservations.Add(
                    new ConstructorParameterRuleObservation(
                        parameter,
                        parameter.Name,
                        ConstructorParameterRuleOrigin.Value,
                        rule.Value,
                        SourceMember: null,
                        destinationMember,
                        IsApplicable: false,
                        ConstructorCandidateRejectionReason.ExplicitRule,
                        rule.DesignatorNode));
                continue;
            }

            parameterObservations.Add(
                new ConstructorParameterRuleObservation(
                    parameter,
                    parameter.Name,
                    ConstructorParameterRuleOrigin.Value,
                    rule.Value,
                    SourceMember: null,
                    destinationMember,
                    IsApplicable: true,
                    ConstructorCandidateRejectionReason.None,
                    rule.DesignatorNode));

            mappedArguments.Add(
                new TypeMapperConstructorArgumentMappingModel(
                    parameter.Name,
                    SourceMemberName: string.Empty,
                    ValueLocalName: null,
                    explicitExpression,
                    ValueLocalTypeName:
                        ConventionConstructorMappingPlanner
                            .BuildExplicitValueLocalTypeName(parameter),
                    TargetTypeName:
                        ConventionConstructorMappingPlanner
                            .BuildTargetValueLocalTypeName(parameter),
                    DependencyExpression:
                        rewrittenDependency?.DependencyExpression,
                    ParameterSymbol: parameter,
                    RuleOriginNode: rule.Value,
                    RuleOrigin:
                        ConstructorParameterRuleOrigin.Value));
        }

        foreach (var parameter in constructor.Parameters)
        {
            if (configuredParameterNames.Contains(parameter.Name))
            {
                continue;
            }

            var sourceMember =
                ConventionConstructorMappingPlanner.TryFindSourceMember(
                    sourceMembers,
                    parameter.Name);
            var compatible = sourceMember is { } candidate &&
                MappingExpressionCompatibility
                    .HasPotentiallyCompatibleConversion(
                        candidate.Type,
                        parameter.Type,
                        compilation);

            if (compatible && sourceMember is { } automaticSource)
            {
                mappedArguments.Add(
                    BuildAutomaticArgument(
                        automaticSource,
                        parameter,
                        originNode: null,
                        ConstructorParameterRuleOrigin.Convention));
                parameterObservations.Add(
                    new ConstructorParameterRuleObservation(
                        parameter,
                        parameter.Name,
                        ConstructorParameterRuleOrigin.Convention,
                        OriginNode: null,
                        automaticSource.Symbol,
                        ConventionConstructorMappingPlanner
                            .FindAssociatedDestinationMember(
                                destinationMembers,
                                parameter.Name),
                        IsApplicable: true,
                        ConstructorCandidateRejectionReason.None));
            }
            else if (ConventionConstructorMappingPlanner.CanOmit(parameter))
            {
                parameterObservations.Add(
                    new ConstructorParameterRuleObservation(
                        parameter,
                        parameter.Name,
                        ConstructorParameterRuleOrigin.Omitted,
                        OriginNode: null,
                        sourceMember?.Symbol,
                        ConventionConstructorMappingPlanner
                            .FindAssociatedDestinationMember(
                                destinationMembers,
                                parameter.Name),
                        IsApplicable: true,
                        ConstructorCandidateRejectionReason.None));
            }
            else
            {
                var ruleRejection = sourceMember is null
                    ? ConstructorCandidateRejectionReason.MissingSourceMember
                    : ConstructorCandidateRejectionReason
                        .IncompatibleArgument;
                Reject(ruleRejection);
                parameterObservations.Add(
                    new ConstructorParameterRuleObservation(
                        parameter,
                        parameter.Name,
                        ConstructorParameterRuleOrigin.Convention,
                        OriginNode: null,
                        sourceMember?.Symbol,
                        ConventionConstructorMappingPlanner
                            .FindAssociatedDestinationMember(
                                destinationMembers,
                                parameter.Name),
                        IsApplicable: false,
                        ruleRejection));
            }
        }

        var argumentArray = mappedArguments.ToImmutable();

        if (rejection == ConstructorCandidateRejectionReason.None &&
            !ConventionConstructorMappingPlanner
                .HasCompatibleAutomaticArguments(
                    sourceType,
                    destination,
                    constructor,
                    argumentArray,
                    compilation,
                    mapperType,
                    cancellationToken))
        {
            rejection = ConstructorCandidateRejectionReason.InvocationBinding;
        }

        if (rejection == ConstructorCandidateRejectionReason.None &&
            !memberMappings.ResultDependentCreationOnlyRules.IsEmpty)
        {
            rejection = ConstructorCandidateRejectionReason
                .ResultDependentInitializer;
        }
        else if (rejection == ConstructorCandidateRejectionReason.None &&
                 !memberMappings.RequiredObligations.IsEmpty &&
                 !ConventionConstructorMappingPlanner
                     .HasSetsRequiredMembersAttribute(constructor))
        {
            rejection = ConstructorCandidateRejectionReason.RequiredMember;
        }

        var plan = rejection == ConstructorCandidateRejectionReason.None
            ? ConventionConstructorMappingPlanner.BuildExplicitPlan(
                destination,
                memberMappings,
                constructor,
                argumentArray,
                compilation,
                mapperType,
                nonNullSourceName,
                cancellationToken)
            : null;

        if (plan is null &&
            rejection == ConstructorCandidateRejectionReason.None)
        {
            rejection = ConstructorCandidateRejectionReason.InvocationBinding;
        }

        return new StructuredConstructorCandidatePlanningResult(
            constructor,
            plan,
            new ConstructorCandidateObservation(
                constructor,
                parameterObservations.ToImmutable(),
                rejection));
    }

    private static TypeMapperConstructorArgumentMappingModel
        BuildAutomaticArgument(
            ConventionReadableMember sourceMember,
            IParameterSymbol parameter,
            SyntaxNode? originNode,
            ConstructorParameterRuleOrigin ruleOrigin)
    {
        return new TypeMapperConstructorArgumentMappingModel(
            parameter.Name,
            sourceMember.Name,
            ValueLocalName: null,
            TargetTypeName:
                ConventionConstructorMappingPlanner
                    .BuildTargetValueLocalTypeName(parameter),
            ParameterSymbol: parameter,
            SourceMemberSymbol: sourceMember.Symbol,
            RuleOriginNode: originNode,
            RuleOrigin: ruleOrigin);
    }

    private static bool TryGetByConventionRules(
        ImmutableArray<StructuredObjectArgument> arguments,
        INamedTypeSymbol destination,
        Compilation compilation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ImmutableArray<StructuredConstructorParameterRule> rules)
    {
        StructuredObjectArgument? markerArgument = null;
        StructuredObjectArgument? parametersArgument = null;

        foreach (var argument in arguments)
        {
            if (IsMarker(
                    argument.Value,
                    ByConventionMarkerMetadataName,
                    semanticModel,
                    cancellationToken))
            {
                if (markerArgument is not null)
                {
                    rules = default;
                    return false;
                }

                markerArgument = argument;
            }
            else
            {
                if (parametersArgument is not null)
                {
                    rules = default;
                    return false;
                }

                parametersArgument = argument;
            }
        }

        if (markerArgument is null)
        {
            rules = default;
            return false;
        }

        if (parametersArgument is not { } parameters ||
            IsOmitted(parameters.Value))
        {
            rules = ImmutableArray<StructuredConstructorParameterRule>.Empty;
            return true;
        }

        if (UnwrapParentheses(parameters.Value) is not
                BaseObjectCreationExpressionSyntax parameterCreation ||
            parameterCreation.ArgumentList?.Arguments.Count > 0 ||
            parameterCreation.Initializer is not { } initializer)
        {
            rules = default;
            return false;
        }

        var constructionModel = ConstructionPlanModelBuilder.Build(
            destination.OriginalDefinition,
            "Morphant.Generated",
            "Construction",
            compilation,
            cancellationToken);
        var parameterNames =
            constructionModel.ConstructorParameterFields.ToDictionary(
                field => field.Name,
                field => field.ParameterName,
                StringComparer.Ordinal);
        var result =
            ImmutableArray.CreateBuilder<
                StructuredConstructorParameterRule>();

        if (parameters.MemberAssignments is
                { } configuredAssignments)
        {
            foreach (var assignment in configuredAssignments)
            {
                if (!parameterNames.TryGetValue(
                        assignment.MemberName,
                        out var parameterName))
                {
                    rules = default;
                    return false;
                }

                result.Add(
                    new StructuredConstructorParameterRule(
                        parameterName,
                        assignment.Value,
                        assignment.DesignatorNode ?? assignment.Value));
            }

            rules = result.ToImmutable();
            return true;
        }

        foreach (var expression in initializer.Expressions)
        {
            if (expression is not AssignmentExpressionSyntax
                {
                    RawKind:
                        (int)SyntaxKind.SimpleAssignmentExpression,
                    Left: IdentifierNameSyntax memberName
                } assignment ||
                !parameterNames.TryGetValue(
                    memberName.Identifier.ValueText,
                    out var parameterName))
            {
                rules = default;
                return false;
            }

            result.Add(
                new StructuredConstructorParameterRule(
                    parameterName,
                    assignment.Right,
                    memberName));
        }

        rules = result.ToImmutable();
        return true;
    }

    private static ImmutableArray<StructuredObjectArgument>
        BuildObjectArguments(
            BaseObjectCreationExpressionSyntax creation)
    {
        return (creation.ArgumentList?.Arguments ?? default)
            .Select(argument =>
                new StructuredObjectArgument(
                    argument,
                    argument.Expression))
            .ToImmutableArray();
    }

    private static bool ContainsMarker(
        ImmutableArray<StructuredObjectArgument> arguments,
        string markerMetadataName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return arguments.Any(argument =>
            IsMarker(
                argument.Value,
                markerMetadataName,
                semanticModel,
                cancellationToken));
    }

    private static bool IsMarker(
        ExpressionSyntax expression,
        string markerMetadataName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        expression = UnwrapParentheses(expression);
        var type = semanticModel.GetTypeInfo(
                expression,
                cancellationToken)
            .Type;

        if (type is null &&
            semanticModel.GetSymbolInfo(
                    expression,
                    cancellationToken)
                .Symbol is IMethodSymbol method)
        {
            type = method.ReturnType;
        }

        return type is INamedTypeSymbol namedType &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       namedType.OriginalDefinition),
                   markerMetadataName);
    }

    private static bool TryGetLambdaParameters(
        LambdaExpressionSyntax lambda,
        SemanticModel semanticModel,
        ResultPolicyForm form,
        CancellationToken cancellationToken,
        out IParameterSymbol sourceParameter,
        out IParameterSymbol? previousParameter,
        out IParameterSymbol? contextParameter)
    {
        var parameters = lambda switch
        {
            SimpleLambdaExpressionSyntax simple =>
                new[] { simple.Parameter },
            ParenthesizedLambdaExpressionSyntax parenthesized =>
                parenthesized.ParameterList.Parameters.ToArray(),
            _ => []
        };
        var hasPrevious = form is
            ResultPolicyForm.SourceAndPrevious or
            ResultPolicyForm.SourcePreviousAndContext;
        var hasContext = form is
            ResultPolicyForm.SourceAndContext or
            ResultPolicyForm.SourcePreviousAndContext;
        var expectedCount = 1 +
            (hasPrevious ? 1 : 0) +
            (hasContext ? 1 : 0);

        if (parameters.Length != expectedCount ||
            semanticModel.GetDeclaredSymbol(
                    parameters[0],
                    cancellationToken) is not
                IParameterSymbol resolvedSource)
        {
            sourceParameter = null!;
            previousParameter = null;
            contextParameter = null;
            return false;
        }

        sourceParameter = resolvedSource;
        var index = 1;
        previousParameter = hasPrevious
            ? semanticModel.GetDeclaredSymbol(
                parameters[index++],
                cancellationToken) as IParameterSymbol
            : null;
        contextParameter = hasContext
            ? semanticModel.GetDeclaredSymbol(
                parameters[index],
                cancellationToken) as IParameterSymbol
            : null;

        return (!hasPrevious || previousParameter is not null) &&
               (!hasContext || contextParameter is not null);
    }

    private static bool IsParameterReference(
        ExpressionSyntax expression,
        IParameterSymbol parameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        while (expression is PostfixUnaryExpressionSyntax
               {
                   RawKind:
                       (int)SyntaxKind.SuppressNullableWarningExpression,
                   Operand: var operand
               })
        {
            expression = UnwrapParentheses(operand);
        }

        return expression is IdentifierNameSyntax identifier &&
               SymbolEqualityComparer.Default.Equals(
                   semanticModel.GetSymbolInfo(
                           identifier,
                           cancellationToken)
                       .Symbol,
               parameter);
    }

    private static bool AreEquivalentPlanNodes(
        StructuredConstructPlanNode left,
        StructuredConstructPlanNode right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is not StructuredConstructLeafNode leftLeaf ||
            right is not StructuredConstructLeafNode rightLeaf ||
            leftLeaf.Kind != rightLeaf.Kind)
        {
            return false;
        }

        if (leftLeaf.Constructor is not { } leftConstructor ||
            rightLeaf.Constructor is not { } rightConstructor)
        {
            return leftLeaf.Constructor is null &&
                   rightLeaf.Constructor is null &&
                   StringComparer.Ordinal.Equals(
                       leftLeaf.Failure?.RecoveryMessage,
                       rightLeaf.Failure?.RecoveryMessage);
        }

        return TypeMapperRuntimeEquality.AreEquivalent(
                   leftConstructor.Constructor,
                   rightConstructor.Constructor) &&
               TypeMapperRuntimeEquality.AreEquivalent(
                   leftConstructor.CreateMemberMappings,
                   rightConstructor.CreateMemberMappings) &&
               TypeMapperRuntimeEquality.AreEquivalent(
                   leftConstructor.CreatePostMemberMappings,
                   rightConstructor.CreatePostMemberMappings);
    }

    private static MappingFailureObservation BuildFailure(
        TypeMapperMappingModel mapping,
        ResultPolicyConfigurationModel configuration,
        MappingFailureReason reason,
        string recoveryMessage)
    {
        return MappingFailureObservation.Create(
            mapping.AnalysisContext,
            reason,
            recoveryMessage,
            MappingObservationOriginKind.Callback,
            new MappingAffectedPath(
                configuration.Kind == ResultPolicyKind.Construct
                    ? MappingExecutionPathSet.NoPrevious
                    : MappingExecutionPathSet.All,
                MappingPlanPhase.ResultSelection),
            configuration.Expression.Syntax,
            configuration.Expression.DeclaringMapperType,
            configuration.Expression.Syntax);
    }

    private static ConstructorPlanningObservation
        ObserveMemberConstraintFailure(
            ConstructorPlanningObservation observation,
            ConstructorInitializationMappingPlan memberMappings)
    {
        var rejection =
            !memberMappings.ResultDependentCreationOnlyRules.IsEmpty
                ? ConstructorCandidateRejectionReason
                    .ResultDependentInitializer
                : !memberMappings.RequiredObligations.IsEmpty
                    ? ConstructorCandidateRejectionReason.RequiredMember
                    : ConstructorCandidateRejectionReason.InvocationBinding;

        return observation with
        {
            Candidates = observation.Candidates.Select(candidate =>
                    observation.SelectedConstructor is not null &&
                    SymbolEqualityComparer.Default.Equals(
                        candidate.Constructor,
                        observation.SelectedConstructor)
                        ? candidate with
                        {
                            RejectionReason = rejection
                        }
                        : candidate)
                .ToImmutableArray()
        };
    }

    private static StructuredConstructLeafNode BuildUnsupportedPlanLeaf(
        TypeMapperMappingModel mapping,
        INamedTypeSymbol sourceMapper,
        SyntaxNode? originNode,
        MappingExecutionPathSet paths,
        MappingFailureReason reason,
        ConstructorPlanningObservation? constructorObservation = null,
        ImmutableArray<DeclarativeTerminalAliasSyntax> terminalAliases =
            default)
    {
        var origin = originNode ??
            mapping.AnalysisContext.Registration.Syntax;
        var affectedPath = new MappingAffectedPath(
            paths,
            MappingPlanPhase.Construction,
            origin);
        var terminal = reason ==
            MappingFailureReason.TerminalNullConstruction
                ? new StructuredTerminalObservation(
                    StructuredTerminalKind.NullConstruction,
                    origin,
                    affectedPath,
                    terminalAliases)
                : null;

        return new StructuredConstructLeafNode(
            StructuredConstructLeafKind.Unsupported,
            Constructor: null,
            ConstructorObservation: constructorObservation,
            Failure: MappingFailureObservation.Create(
                mapping.AnalysisContext,
                reason,
                UnsupportedConstructMessage,
                terminal is null
                    ? MappingObservationOriginKind.Constructor
                    : MappingObservationOriginKind.Callback,
                affectedPath,
                origin,
                sourceMapper,
                origin),
            Terminal: terminal);
    }

    private static TypeMapperControlFlowNode BuildRuntimeNode(
        StructuredConstructPlanNode node,
        TypeMapperMappingModel mapping,
        ConventionMemberMappingPlan memberMappings,
        ImmutableArray<NestedMappingObservation> nestedObservations,
        bool create)
    {
        var retainedNestedObservations =
            mapping.NestedObservations.IsDefault
                ? ImmutableArray<NestedMappingObservation>.Empty
                : mapping.NestedObservations;
        mapping = mapping with
        {
            NestedObservations = retainedNestedObservations.AddRange(
                nestedObservations)
        };
        var leaf = (StructuredConstructLeafNode)node;

        if (leaf.Failure is { } leafFailure &&
            !nestedObservations.IsDefaultOrEmpty)
        {
            var nestedObservation = nestedObservations.FirstOrDefault(
                static observation => observation.FailureKind !=
                    NestedMappingFailureKind.None);

            if (nestedObservation is null)
            {
                return BuildRuntimeNodeWithoutNestedFailure(
                    leaf,
                    mapping,
                    memberMappings,
                    create);
            }

            var nestedReason = ClassifyNestedFailure(
                nestedObservation,
                leafFailure.Reason);

            leaf = leaf with
            {
                Failure = nestedReason == leafFailure.Reason
                    ? leafFailure with
                    {
                        NestedObservations = nestedObservations
                    }
                    : leafFailure with
                    {
                        Reason = nestedReason,
                        OriginKind = MappingObservationOriginKind.NestedMarker,
                        OffendingNode = nestedObservation.Producer,
                        OffendingSymbol = nestedObservation.ProducerSymbol,
                        PrimaryLocation = nestedObservation.Producer
                            .GetLocation(),
                        AffectedPath = leafFailure.AffectedPath with
                        {
                            Phase = MappingPlanPhase.NestedMapping,
                            BranchOrigin = nestedObservation.Producer
                        },
                        NestedObservations = nestedObservations
                    }
            };
        }

        return BuildRuntimeNodeWithoutNestedFailure(
            leaf,
            mapping,
            memberMappings,
            create);
    }

    private static TypeMapperControlFlowNode
        BuildRuntimeNodeWithoutNestedFailure(
            StructuredConstructLeafNode leaf,
            TypeMapperMappingModel mapping,
            ConventionMemberMappingPlan memberMappings,
            bool create)
    {
        return leaf.Kind switch
        {
            StructuredConstructLeafKind.Constructor
                when leaf.Constructor is { } constructor =>
                BuildConstructorLeaf(mapping, constructor),
            StructuredConstructLeafKind.Previous =>
                BuildPreviousLeaf(
                    mapping,
                    memberMappings,
                    create,
                    leaf.Terminal),
            _ => BuildUnsupportedLeaf(
                mapping,
                create,
                leaf.Failure ?? MappingFailureObservation.Create(
                    mapping.AnalysisContext,
                    MappingFailureReason.UnsupportedStructuredSyntax,
                    UnsupportedConstructMessage,
                    MappingObservationOriginKind.Constructor,
                    create
                        ? MappingAffectedPath.NoPrevious(
                            MappingPlanPhase.Construction)
                        : MappingAffectedPath.ExistingDestination(
                            MappingPlanPhase.Construction)),
                leaf.Terminal,
                leaf.ConstructorObservation)
        };
    }

    private static TypeMapperControlFlowNode BuildConstructorLeaf(
        TypeMapperMappingModel mapping,
        ConventionConstructorMappingPlan constructor)
    {
        var leaf = mapping with
        {
            CreateConstructor = constructor.Constructor,
            CreateMemberMappings =
                constructor.CreateMemberMappings,
            CreatePostMemberMappings =
                constructor.CreatePostMemberMappings,
            UpdateMemberMappings = ImmutableArray<TypeMapperMemberMappingModel>.Empty,
            ControlFlow = null,
            CreateFailure = null,
            UpdateFailure = null,
            Failure = null,
            ConstructorObservation = constructor.Observation ??
                mapping.ConstructorObservation
        };

        return new TypeMapperControlFlowNode(
            Locals: ImmutableArray<TypeMapperLocalValueModel>.Empty,
            Condition: null,
            WhenTrue: null,
            WhenFalse: null,
            Leaf: leaf,
            ThrowExpression: null);
    }

    private static MappingFailureReason ClassifyNestedFailure(
        NestedMappingObservation observation,
        MappingFailureReason fallback)
    {
        return observation.FailureKind switch
        {
            NestedMappingFailureKind.SourceTypeUnknown or
            NestedMappingFailureKind.ParameterlessSourceUnavailable or
            NestedMappingFailureKind.DestinationTypeUnknown =>
                MappingFailureReason.NestedPairUnknown,
            NestedMappingFailureKind.ResultIncompatible =>
                MappingFailureReason.NestedResultIncompatible,
            NestedMappingFailureKind.ExplicitDestinationIncompatible or
            NestedMappingFailureKind.ExplicitNullForNonNullableValue or
            NestedMappingFailureKind.AdaptiveCurrentUnavailable or
            NestedMappingFailureKind.AdaptiveCurrentIncompatible or
            NestedMappingFailureKind.AdaptiveCurrentAmbiguous or
            NestedMappingFailureKind.ReadOnlyProxyInvalid =>
                MappingFailureReason.NestedUpdateDestinationInvalid,
            _ => fallback
        };
    }

    private static TypeMapperControlFlowNode BuildPreviousLeaf(
        TypeMapperMappingModel mapping,
        ConventionMemberMappingPlan memberMappings,
        bool create,
        StructuredTerminalObservation? terminal = null)
    {
        if (create)
        {
            var origin = terminal?.OriginNode ??
                mapping.AnalysisContext.Registration.Syntax;

            return BuildUnsupportedLeaf(
                mapping,
                create: true,
                MappingFailureObservation.Create(
                    mapping.AnalysisContext,
                    MappingFailureReason.TerminalPreviousWithoutValue,
                    UnavailablePreviousMessage,
                    MappingObservationOriginKind.Callback,
                    MappingAffectedPath.NoPrevious(
                        MappingPlanPhase.Construction) with
                    {
                        BranchOrigin = origin
                    },
                    originNode: origin,
                    offendingNode: origin),
                terminal);
        }

        var terminals = mapping.StructuredTerminals.IsDefault
            ? ImmutableArray<StructuredTerminalObservation>.Empty
            : mapping.StructuredTerminals;
        var leaf = mapping with
        {
            CreateConstructor = null,
            CreateMemberMappings = ImmutableArray<TypeMapperMemberMappingModel>.Empty,
            CreatePostMemberMappings = ImmutableArray<TypeMapperMemberMappingModel>.Empty,
            UpdateMemberMappings = memberMappings.Update,
            ControlFlow = null,
            CreateFailure = null,
            UpdateFailure = null,
            Failure = null,
            StructuredTerminals = terminal is null
                ? terminals
                : terminals.Add(terminal)
        };

        return new TypeMapperControlFlowNode(
            Locals: ImmutableArray<TypeMapperLocalValueModel>.Empty,
            Condition: null,
            WhenTrue: null,
            WhenFalse: null,
            Leaf: leaf,
            ThrowExpression: null);
    }

    private static TypeMapperControlFlowNode BuildUnsupportedLeaf(
        TypeMapperMappingModel mapping,
        bool create,
        MappingFailureObservation failure,
        StructuredTerminalObservation? terminal = null,
        ConstructorPlanningObservation? constructorObservation = null)
    {
        var terminals = mapping.StructuredTerminals.IsDefault
            ? ImmutableArray<StructuredTerminalObservation>.Empty
            : mapping.StructuredTerminals;

        if (terminal is not null &&
            constructorObservation is { } observedConstructor)
        {
            var constructorTerminals = observedConstructor.Terminals.IsDefault
                ? ImmutableArray<StructuredTerminalObservation>.Empty
                : observedConstructor.Terminals;
            constructorObservation = observedConstructor with
            {
                Terminals = constructorTerminals.Add(terminal)
            };
        }

        var leaf = mapping with
        {
            ControlFlow = null,
            CreateFailure =
                create ? failure : null,
            UpdateFailure =
                create ? null : failure,
            Failure = null,
            ConstructorObservation = constructorObservation ??
                mapping.ConstructorObservation,
            StructuredTerminals = terminal is null
                ? terminals
                : terminals.Add(terminal)
        };

        return new TypeMapperControlFlowNode(
            Locals: ImmutableArray<TypeMapperLocalValueModel>.Empty,
            Condition: null,
            WhenTrue: null,
            WhenFalse: null,
            Leaf: leaf,
            ThrowExpression: null);
    }

    private static string Identifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value
            : value;
    }

    private static bool IsOmitted(ExpressionSyntax expression)
    {
        return TryGetOmittedProducer(expression, out _);
    }

    private static bool TryGetOmittedProducer(
        ExpressionSyntax expression,
        out ExpressionSyntax producer)
    {
        expression = DeclarativeIntrinsic.UnwrapTransparentSyntax(expression);

        if (expression is CastExpressionSyntax cast &&
            TryGetOmittedProducer(cast.Expression, out producer))
        {
            return true;
        }

        if (expression is
            LiteralExpressionSyntax
            {
                RawKind:
                    (int)SyntaxKind.NullLiteralExpression or
                    (int)SyntaxKind.DefaultLiteralExpression
            } or
            DefaultExpressionSyntax)
        {
            producer = expression;
            return true;
        }

        producer = null!;
        return false;
    }

    private static ExpressionSyntax UnwrapParentheses(
        ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}

internal readonly record struct StructuredObjectArgument(
    ArgumentSyntax Syntax,
    ExpressionSyntax Value,
    ImmutableArray<DeclarativeMemberAssignmentSyntax>?
        MemberAssignments = null);

internal readonly record struct StructuredConstructorParameterRule(
    string ParameterName,
    ExpressionSyntax Value,
    SyntaxNode DesignatorNode);

internal readonly record struct StructuredConstructorCandidatePlanningResult(
    IMethodSymbol Constructor,
    ConventionConstructorMappingPlan? Plan,
    ConstructorCandidateObservation Observation);

internal readonly record struct StructuredConstructMappingResult(
    TypeMapperControlFlowMappingModel? ControlFlow,
    ImmutableArray<string> HelperMethodDeclarations,
    MappingFailureObservation? Failure)
{
    public static StructuredConstructMappingResult Unsupported(
        MappingFailureObservation failure) =>
        new(
            ControlFlow: null,
            HelperMethodDeclarations: ImmutableArray<string>.Empty,
            Failure: failure);
}

internal abstract record StructuredConstructPlanNode;

internal sealed record StructuredConstructLeafNode(
    StructuredConstructLeafKind Kind,
    ConventionConstructorMappingPlan? Constructor,
    ConstructorPlanningObservation? ConstructorObservation,
    MappingFailureObservation? Failure,
    StructuredTerminalObservation? Terminal)
    : StructuredConstructPlanNode;

internal enum StructuredConstructLeafKind
{
    Constructor,
    Previous,
    Unsupported
}
