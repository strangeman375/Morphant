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
        "The configured structured result callback cannot be represented " +
        "by the supported declarative grammar.";

    private const string UnavailablePreviousMessage =
        "The configured structured result callback selected an unavailable " +
        "previous destination.";

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
                cancellationToken))
        {
            return StructuredConstructMappingResult.Unsupported(
                UnsupportedConstructMessage);
        }

        var transferScope = (SyntaxNode?)lambda.ExpressionBody ??
                            lambda.Block;

        if (transferScope is null)
        {
            return StructuredConstructMappingResult.Unsupported(
                UnsupportedConstructMessage);
        }

        if (DeclarativeControlFlowPlanner.Build(
                lambda,
                configuration.Expression.SemanticModel,
                cancellationToken) is not
            DeclarativeControlFlowProgram controlFlowProgram)
        {
            return StructuredConstructMappingResult.Unsupported(
                UnsupportedConstructMessage);
        }

        TypeMapperControlFlowNode? BuildPlan(
            bool? previousAvailable)
        {
            var nestedMapUsages =
                new DeclarativeNestedMapUsageRegistry();
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
                var parameterType = parameter.Type.WithNullableAnnotation(
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
                        currentDestination));
            }

            StructuredConstructPlanNode? BuildExpression(
                ExpressionSyntax expression) =>
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
                        Equals(whenTrue, whenFalse)
                            ? whenTrue
                            : null,
                    previousParameter,
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
                        ? BuildExpression(directExpression)
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

                        plannedLeaf = convention is null
                            ? new StructuredConstructLeafNode(
                                StructuredConstructLeafKind.Unsupported,
                                Constructor: null,
                                UnsupportedMessage:
                                    UnsupportedConstructMessage)
                            : new StructuredConstructLeafNode(
                                StructuredConstructLeafKind.Constructor,
                                convention,
                                UnsupportedMessage: null);
                    }
                    else
                    {
                        var explicitPlan =
                            ExplicitStructuredConstructorPlanner.Build(
                                arguments,
                                sourceType,
                                destination,
                                compilation,
                                mapperType,
                                configuration.Expression.SemanticModel,
                                Rewrite,
                                RewriteDependency,
                                cancellationToken);

                        plannedLeaf = explicitPlan is null
                            ? StructuredConstructLeafNode.Unsupported
                            : ConventionConstructorMappingPlanner
                                .BuildExplicitPlan(
                                    destination,
                                    constructorMembers,
                                    explicitPlan.Value.Constructor,
                                    explicitPlan.Value.Arguments,
                                    mapperType,
                                    mapping.NonNullSourceName) is
                                { } constructor
                                ? new StructuredConstructLeafNode(
                                    StructuredConstructLeafKind.Constructor,
                                    constructor,
                                    UnsupportedMessage: null)
                                : StructuredConstructLeafNode.Unsupported;
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
                    UnsupportedConstructMessage);
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
                    UnsupportedConstructMessage);
            }

            createRoot = createPlan;
            updateRoot = updatePlan;
        }

        return new StructuredConstructMappingResult(
            new TypeMapperControlFlowMappingModel(
                createRoot,
                updateRoot),
            HelperMethodDeclarations: [],
            UnsupportedMessage: null);
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

        return Equals(whenTrue, whenFalse)
            ? new TypeMapperControlFlowNode(
                Locals: [],
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
                Locals: [],
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
            return StructuredConstructLeafNode.Previous;
        }

        if (expression is not BaseObjectCreationExpressionSyntax creation ||
            creation.Initializer is not null)
        {
            return StructuredConstructLeafNode.Unsupported;
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

            return convention is null
                ? new StructuredConstructLeafNode(
                    StructuredConstructLeafKind.Unsupported,
                    Constructor: null,
                    UnsupportedMessage: UnsupportedConstructMessage)
                : new StructuredConstructLeafNode(
                    StructuredConstructLeafKind.Constructor,
                    convention,
                    UnsupportedMessage: null);
        }

        var explicitPlan =
            ExplicitStructuredConstructorPlanner.Build(
                arguments,
                sourceType,
                destination,
                compilation,
                mapperType,
                semanticModel,
                rewriteExpression,
                rewriteDependencyExpression,
                cancellationToken);

        if (explicitPlan is null)
        {
            return StructuredConstructLeafNode.Unsupported;
        }

        var constructor =
            ConventionConstructorMappingPlanner.BuildExplicitPlan(
                destination,
                memberMappings,
                explicitPlan.Value.Constructor,
                explicitPlan.Value.Arguments,
                mapperType,
                nonNullSourceName);

        return constructor is null
            ? StructuredConstructLeafNode.Unsupported
            : new StructuredConstructLeafNode(
                StructuredConstructLeafKind.Constructor,
                constructor,
                UnsupportedMessage: null);
    }

    private static ConventionConstructorMappingPlan?
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

        if (constructorSelection is null ||
            !TryGetByConventionRules(
                arguments,
                destination,
                compilation,
                semanticModel,
                cancellationToken,
                out var rules))
        {
            return null;
        }

        if (rules.IsEmpty)
        {
            return ConventionConstructorMappingPlanner.Build(
                sourceType,
                destination,
                memberMappings,
                capabilities,
                constructorSelection,
                compilation,
                mapperType,
                nonNullSourceName,
                cancellationToken);
        }

        var sourceMembers =
            ConventionMemberMappingPlanner.BuildReadableMembers(
                sourceType,
                compilation,
                mapperType,
                cancellationToken);

        if (constructorSelection ==
            ConstructorSelectionValue.Greediest)
        {
            return ConventionConstructorMappingPlanner
                .TrySelectGreediestPlan(
                    constructors,
                    candidate => BuildByConventionPlanForConstructor(
                        candidate,
                        sourceType,
                        rules,
                        sourceMembers,
                        destination,
                        memberMappings,
                        compilation,
                        mapperType,
                        semanticModel,
                        nonNullSourceName,
                        rewriteExpression,
                        rewriteDependencyExpression,
                        cancellationToken),
                    cancellationToken);
        }

        if (ConventionConstructorMappingPlanner.TrySelectConstructor(
                constructors,
                constructorSelection.Value) is not { } constructor)
        {
            return null;
        }

        return BuildByConventionPlanForConstructor(
            constructor,
            sourceType,
            rules,
            sourceMembers,
            destination,
            memberMappings,
            compilation,
            mapperType,
            semanticModel,
            nonNullSourceName,
            rewriteExpression,
            rewriteDependencyExpression,
            cancellationToken);
    }

    private static ConventionConstructorMappingPlan?
        BuildByConventionPlanForConstructor(
            IMethodSymbol constructor,
            ITypeSymbol sourceType,
            ImmutableArray<StructuredConstructorParameterRule> rules,
            ImmutableArray<ConventionReadableMember> sourceMembers,
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

        foreach (var rule in rules)
        {
            var parameter = constructor.Parameters.FirstOrDefault(
                candidate => StringComparer.Ordinal.Equals(
                    candidate.Name,
                    rule.ParameterName));

            if (parameter is null ||
                !configuredParameterNames.Add(parameter.Name))
            {
                return null;
            }

            if (DeclarativeConstructorMarker.TryGetKind(
                    rule.Value,
                    semanticModel,
                    cancellationToken,
                    out var markerKind))
            {
                if (markerKind ==
                    DeclarativeConstructorMarkerKind.Ignore)
                {
                    if (!ConventionConstructorMappingPlanner.CanOmit(
                            parameter))
                    {
                        return null;
                    }

                    continue;
                }

                if (markerKind ==
                    DeclarativeConstructorMarkerKind.Auto)
                {
                    if (!TryBuildAutomaticArgument(
                            sourceMembers,
                            parameter,
                            compilation,
                            out var automaticArgument))
                    {
                        return null;
                    }

                    mappedArguments.Add(automaticArgument);
                    continue;
                }
            }

            var rewrittenDependency =
                rewriteDependencyExpression(
                    rule.Value,
                    parameter);
            var explicitExpression =
                rewrittenDependency?.Expression ??
                ExplicitStructuredConstructorPlanner
                    .RewriteArgumentExpression(
                        rule.Value,
                        parameter,
                        compilation,
                        semanticModel,
                        rewriteExpression,
                        cancellationToken);

            if (explicitExpression is null)
            {
                return null;
            }

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
                        rewrittenDependency?.DependencyExpression));
        }

        foreach (var parameter in constructor.Parameters)
        {
            if (configuredParameterNames.Contains(parameter.Name))
            {
                continue;
            }

            if (TryBuildAutomaticArgument(
                    sourceMembers,
                    parameter,
                    compilation,
                    out var automaticArgument))
            {
                mappedArguments.Add(automaticArgument);
            }
            else if (!ConventionConstructorMappingPlanner.CanOmit(parameter))
            {
                return null;
            }
        }

        var argumentArray = mappedArguments.ToImmutable();

        if (!ConventionConstructorMappingPlanner
                .HasCompatibleAutomaticArguments(
                    sourceType,
                    destination,
                    constructor,
                    argumentArray,
                    compilation,
                    mapperType,
                    cancellationToken))
        {
            return null;
        }

        return ConventionConstructorMappingPlanner.BuildExplicitPlan(
            destination,
            memberMappings,
            constructor,
            argumentArray,
            mapperType,
            nonNullSourceName);
    }

    private static bool TryBuildAutomaticArgument(
        ImmutableArray<ConventionReadableMember> sourceMembers,
        IParameterSymbol parameter,
        CSharpCompilation compilation,
        out TypeMapperConstructorArgumentMappingModel argument)
    {
        if (ConventionConstructorMappingPlanner.TryFindSourceMember(
                sourceMembers,
                parameter.Name) is not { } sourceMember ||
            !MappingExpressionCompatibility
                .HasPotentiallyCompatibleConversion(
                    sourceMember.Type,
                    parameter.Type,
                    compilation))
        {
            argument = default;
            return false;
        }

        argument = new TypeMapperConstructorArgumentMappingModel(
            parameter.Name,
            sourceMember.Name,
            ValueLocalName: null,
            TargetTypeName:
                ConventionConstructorMappingPlanner
                    .BuildTargetValueLocalTypeName(parameter));
        return true;
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
            rules = [];
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
                        assignment.Value));
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
                    assignment.Right));
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

    private static TypeMapperControlFlowNode BuildRuntimeNode(
        StructuredConstructPlanNode node,
        TypeMapperMappingModel mapping,
        ConventionMemberMappingPlan memberMappings,
        bool create)
    {
        var leaf = (StructuredConstructLeafNode)node;

        return leaf.Kind switch
        {
            StructuredConstructLeafKind.Constructor
                when leaf.Constructor is { } constructor =>
                BuildConstructorLeaf(mapping, constructor),
            StructuredConstructLeafKind.Previous =>
                BuildPreviousLeaf(
                    mapping,
                    memberMappings,
                    create),
            _ => BuildUnsupportedLeaf(
                mapping,
                create,
                leaf.UnsupportedMessage ?? UnsupportedConstructMessage)
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
            UpdateMemberMappings = [],
            ControlFlow = null,
            CreateUnsupportedExceptionMessage = null,
            UpdateUnsupportedExceptionMessage = null,
            UnsupportedExceptionMessage = null
        };

        return new TypeMapperControlFlowNode(
            Locals: [],
            Condition: null,
            WhenTrue: null,
            WhenFalse: null,
            Leaf: leaf,
            ThrowExpression: null);
    }

    private static TypeMapperControlFlowNode BuildPreviousLeaf(
        TypeMapperMappingModel mapping,
        ConventionMemberMappingPlan memberMappings,
        bool create)
    {
        if (create)
        {
            return BuildUnsupportedLeaf(
                mapping,
                create: true,
                UnavailablePreviousMessage);
        }

        var leaf = mapping with
        {
            CreateConstructor = null,
            CreateMemberMappings = [],
            CreatePostMemberMappings = [],
            UpdateMemberMappings = memberMappings.Update,
            ControlFlow = null,
            CreateUnsupportedExceptionMessage = null,
            UpdateUnsupportedExceptionMessage = null,
            UnsupportedExceptionMessage = null
        };

        return new TypeMapperControlFlowNode(
            Locals: [],
            Condition: null,
            WhenTrue: null,
            WhenFalse: null,
            Leaf: leaf,
            ThrowExpression: null);
    }

    private static TypeMapperControlFlowNode BuildUnsupportedLeaf(
        TypeMapperMappingModel mapping,
        bool create,
        string message)
    {
        var leaf = mapping with
        {
            ControlFlow = null,
            CreateUnsupportedExceptionMessage =
                create ? message : null,
            UpdateUnsupportedExceptionMessage =
                create ? null : message,
            UnsupportedExceptionMessage = null
        };

        return new TypeMapperControlFlowNode(
            Locals: [],
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
        return UnwrapParentheses(expression) is
            LiteralExpressionSyntax
            {
                RawKind:
                    (int)SyntaxKind.NullLiteralExpression or
                    (int)SyntaxKind.DefaultLiteralExpression
            } or
            DefaultExpressionSyntax;
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
    ExpressionSyntax Value);

internal readonly record struct StructuredConstructMappingResult(
    TypeMapperControlFlowMappingModel? ControlFlow,
    ImmutableArray<string> HelperMethodDeclarations,
    string? UnsupportedMessage)
{
    public static StructuredConstructMappingResult Unsupported(
        string message) =>
        new(
            ControlFlow: null,
            HelperMethodDeclarations: [],
            UnsupportedMessage: message);
}

internal abstract record StructuredConstructPlanNode;

internal sealed record StructuredConstructLeafNode(
    StructuredConstructLeafKind Kind,
    ConventionConstructorMappingPlan? Constructor,
    string? UnsupportedMessage)
    : StructuredConstructPlanNode
{
    public static StructuredConstructLeafNode Previous { get; } =
        new(
            StructuredConstructLeafKind.Previous,
            Constructor: null,
            UnsupportedMessage: null);

    public static StructuredConstructLeafNode Unsupported { get; } =
        new(
            StructuredConstructLeafKind.Unsupported,
            Constructor: null,
            UnsupportedConstructMappingMessage.Value);

    private static class UnsupportedConstructMappingMessage
    {
        public const string Value =
            "The configured structured Construct callback cannot be " +
            "represented by the supported declarative grammar.";
    }
}

internal enum StructuredConstructLeafKind
{
    Constructor,
    Previous,
    Unsupported
}
