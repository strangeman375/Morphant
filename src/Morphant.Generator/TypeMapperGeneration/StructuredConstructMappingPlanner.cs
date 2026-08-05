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
        "The configured structured Construct is not supported yet.";

    private const string UnavailablePreviousMessage =
        "The configured Construct selected an unavailable previous destination.";

    private const string ConstructorSelectionUnsupportedMessage =
        "The effective ConstructorSelection is not supported yet.";

    private const string ByConventionMarkerMetadataName =
        "Morphant.Markers.ByConventionMarker";

    public static StructuredConstructMappingResult Build(
        ConstructConfigurationModel configuration,
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
                out var previousParameter) is false)
        {
            return StructuredConstructMappingResult.Unsupported(
                UnsupportedConstructMessage);
        }

        var factoryHelperRegistry =
            new ByFactoryHelperRegistry(usedGeneratedMethodNames);

        var transferScope = (SyntaxNode?)lambda.ExpressionBody ??
                            lambda.Block;

        if (transferScope is null)
        {
            return StructuredConstructMappingResult.Unsupported(
                UnsupportedConstructMessage);
        }

        StructuredConstructPlanNode? BuildPlan(
            bool? previousAvailable)
        {
            PreviousExpressionSubstitution? previousSubstitution =
                previousParameter is not null &&
                previousAvailable is { } hasPrevious
                    ? BuildPreviousSubstitution(
                        mapping,
                        hasPrevious)
                    : null;

            string? Rewrite(ExpressionSyntax expression)
            {
                return ConstructExpressionRewriter.TryRewrite(
                        expression,
                        configuration.Expression.SemanticModel,
                        mapperType,
                        sourceParameter,
                        mapping.NonNullSourceName,
                        previousParameter,
                        previousSubstitution,
                        transferScope,
                        cancellationToken,
                        out var rewritten)
                    ? rewritten
                    : null;
            }

            StructuredConstructPlanNode? BuildCondition(
                ExpressionSyntax condition,
                StructuredConstructPlanNode whenTrue,
                StructuredConstructPlanNode whenFalse) =>
                BuildConditionNode(
                    condition,
                    whenTrue,
                    whenFalse,
                    Rewrite,
                    previousParameter,
                    previousAvailable,
                    configuration.Expression.SemanticModel,
                    cancellationToken);

            StructuredConstructLeafNode? BuildFactory(
                ImmutableArray<StructuredObjectArgument> arguments)
            {
                if (!ByFactoryMappingPlanner.TryBuild(
                        arguments,
                        mapping,
                        memberMappings.MapExisting,
                        configuration.Expression.SemanticModel,
                        mapperType,
                        sourceParameter,
                        previousParameter,
                        previousSubstitution,
                        transferScope,
                        factoryHelperRegistry,
                        cancellationToken,
                        out var factory,
                        out var unsupportedMessage))
                {
                    return null;
                }

                return factory is { } factoryValue
                    ? new StructuredConstructLeafNode(
                        StructuredConstructLeafKind.Factory,
                        Constructor: null,
                        Factory: factoryValue,
                        UnsupportedMessage: null)
                    : new StructuredConstructLeafNode(
                        StructuredConstructLeafKind.Unsupported,
                        Constructor: null,
                        Factory: null,
                        unsupportedMessage ??
                        UnsupportedConstructMessage);
            }

            StructuredConstructPlanNode? BuildExpression(
                ExpressionSyntax expression) =>
                BuildPlanNode(
                    expression,
                    sourceType,
                    destination,
                    capabilities,
                    memberMappings,
                    constructorSelection,
                    compilation,
                    mapperType,
                    configuration.Expression.SemanticModel,
                    mapping.NonNullSourceName,
                    Rewrite,
                    BuildCondition,
                    BuildFactory,
                    previousParameter,
                    cancellationToken);

            return lambda.ExpressionBody is { } resultExpression
                ? BuildExpression(resultExpression)
                : BuildPlanStatements(
                    lambda.Block!.Statements,
                    continuation: null,
                    BuildExpression,
                    BuildCondition);
        }

        TypeMapperControlFlowNode mapNewRoot;
        TypeMapperControlFlowNode mapExistingRoot;

        if (configuration.Form == ConstructConfigurationForm.Source)
        {
            var plannedRoot = BuildPlan(previousAvailable: null);

            if (plannedRoot is null)
            {
                factoryHelperRegistry.Rollback();
                return StructuredConstructMappingResult.Unsupported(
                    UnsupportedConstructMessage);
            }

            mapNewRoot = BuildRuntimeNode(
                plannedRoot,
                mapping,
                memberMappings,
                mapNew: true);
            mapExistingRoot = BuildPreviousLeaf(
                mapping,
                memberMappings,
                mapNew: false);
        }
        else
        {
            var mapNewPlan = BuildPlan(previousAvailable: false);
            var mapExistingPlan = BuildPlan(previousAvailable: true);

            if (mapNewPlan is null || mapExistingPlan is null)
            {
                factoryHelperRegistry.Rollback();
                return StructuredConstructMappingResult.Unsupported(
                    UnsupportedConstructMessage);
            }

            mapNewRoot = BuildRuntimeNode(
                mapNewPlan,
                mapping,
                memberMappings,
                mapNew: true);
            mapExistingRoot = BuildRuntimeNode(
                mapExistingPlan,
                mapping,
                memberMappings,
                mapNew: false);
        }

        return new StructuredConstructMappingResult(
            new TypeMapperControlFlowMappingModel(
                mapNewRoot,
                mapExistingRoot),
            factoryHelperRegistry.HelperMethodDeclarations,
            UnsupportedMessage: null);
    }

    private static StructuredConstructPlanNode? BuildPlanStatements(
        SyntaxList<StatementSyntax> statements,
        StructuredConstructPlanNode? continuation,
        Func<ExpressionSyntax, StructuredConstructPlanNode?> buildExpression,
        Func<
            ExpressionSyntax,
            StructuredConstructPlanNode,
            StructuredConstructPlanNode,
            StructuredConstructPlanNode?> buildCondition)
    {
        var result = continuation;

        for (var index = statements.Count - 1;
             index >= 0;
             index--)
        {
            result = BuildPlanStatement(
                statements[index],
                result,
                buildExpression,
                buildCondition);

            if (result is null)
            {
                return null;
            }
        }

        return result;
    }

    private static StructuredConstructPlanNode? BuildPlanStatement(
        StatementSyntax statement,
        StructuredConstructPlanNode? continuation,
        Func<ExpressionSyntax, StructuredConstructPlanNode?> buildExpression,
        Func<
            ExpressionSyntax,
            StructuredConstructPlanNode,
            StructuredConstructPlanNode,
            StructuredConstructPlanNode?> buildCondition)
    {
        switch (statement)
        {
            case ReturnStatementSyntax
            {
                Expression: { } expression
            }:
                return buildExpression(expression);

            case BlockSyntax block:
                return BuildPlanStatements(
                    block.Statements,
                    continuation,
                    buildExpression,
                    buildCondition);

            case IfStatementSyntax ifStatement:
            {
                var whenTrue = BuildPlanStatement(
                    ifStatement.Statement,
                    continuation,
                    buildExpression,
                    buildCondition);
                var whenFalse = ifStatement.Else is { } elseClause
                    ? BuildPlanStatement(
                        elseClause.Statement,
                        continuation,
                        buildExpression,
                        buildCondition)
                    : continuation;

                return whenTrue is null || whenFalse is null
                    ? null
                    : buildCondition(
                        ifStatement.Condition,
                        whenTrue,
                        whenFalse);
            }

            default:
                return null;
        }
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

    private static StructuredConstructPlanNode? BuildConditionNode(
        ExpressionSyntax condition,
        StructuredConstructPlanNode whenTrue,
        StructuredConstructPlanNode whenFalse,
        Func<ExpressionSyntax, string?> rewriteExpression,
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
                return BuildConditionNode(
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
                        var whenLeftTrue = BuildConditionNode(
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
                            : BuildConditionNode(
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
                        var whenLeftFalse = BuildConditionNode(
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
                            : BuildConditionNode(
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
            ? new StructuredConstructEvaluationNode(
                rewrittenCondition,
                whenTrue)
            : new StructuredConstructConditionalNode(
                rewrittenCondition,
                whenTrue,
                whenFalse);
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
        ConventionMemberMappingPlan memberMappings,
        ConstructorSelectionValue? constructorSelection,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        SemanticModel semanticModel,
        string nonNullSourceName,
        Func<ExpressionSyntax, string?> rewriteExpression,
        Func<
            ExpressionSyntax,
            StructuredConstructPlanNode,
            StructuredConstructPlanNode,
            StructuredConstructPlanNode?> buildCondition,
        Func<
            ImmutableArray<StructuredObjectArgument>,
            StructuredConstructLeafNode?> buildFactory,
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
                buildCondition,
                buildFactory,
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
                buildCondition,
                buildFactory,
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
                cancellationToken);

            return convention is null
                ? new StructuredConstructLeafNode(
                    StructuredConstructLeafKind.Unsupported,
                    Constructor: null,
                    Factory: null,
                    UnsupportedMessage: constructorSelection ==
                        ConstructorSelectionValue.Unambiguous
                        ? UnsupportedConstructMessage
                        : ConstructorSelectionUnsupportedMessage)
                : new StructuredConstructLeafNode(
                    StructuredConstructLeafKind.Constructor,
                    convention,
                    Factory: null,
                    UnsupportedMessage: null);
        }

        if (buildFactory(arguments) is { } factory)
        {
            return factory;
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
                Factory: null,
                UnsupportedMessage: null);
    }

    private static ConventionConstructorMappingPlan?
        BuildByConventionPlan(
            ImmutableArray<StructuredObjectArgument> arguments,
            ITypeSymbol sourceType,
            INamedTypeSymbol destination,
            MappingPairCapabilities capabilities,
            ConventionMemberMappingPlan memberMappings,
            ConstructorSelectionValue? constructorSelection,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            SemanticModel semanticModel,
            string nonNullSourceName,
            Func<ExpressionSyntax, string?> rewriteExpression,
            CancellationToken cancellationToken)
    {
        if (constructorSelection !=
            ConstructorSelectionValue.Unambiguous)
        {
            return null;
        }

        var constructors =
            DestinationCapabilityPolicy.GetSupportedConstructors(
                destination,
                compilation,
                cancellationToken);
        var constructor =
            ConventionConstructorMappingPlanner.TrySelectConstructor(
                constructors);

        if (constructor is null ||
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
                    DeclarativeConstructorMarkerKind.Map)
                {
                    return null;
                }

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

            var explicitExpression =
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
                            .BuildTargetValueLocalTypeName(parameter)));
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

        return ConventionConstructorMappingPlanner.BuildExplicitPlan(
            destination,
            memberMappings,
            constructor,
            mappedArguments.ToImmutable(),
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
        ConstructConfigurationForm form,
        CancellationToken cancellationToken,
        out IParameterSymbol sourceParameter,
        out IParameterSymbol? previousParameter)
    {
        var parameters = lambda switch
        {
            SimpleLambdaExpressionSyntax simple =>
                new[] { simple.Parameter },
            ParenthesizedLambdaExpressionSyntax parenthesized =>
                parenthesized.ParameterList.Parameters.ToArray(),
            _ => []
        };
        var expectedCount = form == ConstructConfigurationForm.Source
            ? 1
            : 2;

        if (parameters.Length != expectedCount ||
            semanticModel.GetDeclaredSymbol(
                    parameters[0],
                    cancellationToken) is not
                IParameterSymbol resolvedSource)
        {
            sourceParameter = null!;
            previousParameter = null;
            return false;
        }

        sourceParameter = resolvedSource;

        if (expectedCount == 1)
        {
            previousParameter = null;
            return true;
        }

        previousParameter = semanticModel.GetDeclaredSymbol(
                parameters[1],
                cancellationToken) as IParameterSymbol;
        return previousParameter is not null;
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
        bool mapNew)
    {
        if (node is StructuredConstructEvaluationNode evaluation)
        {
            return new TypeMapperControlFlowNode(
                Locals: [],
                Condition: null,
                WhenTrue: null,
                WhenFalse: null,
                Leaf: null,
                ThrowExpression: null,
                EvaluationExpression: evaluation.Expression,
                EvaluationContinuation: BuildRuntimeNode(
                    evaluation.Continuation,
                    mapping,
                    memberMappings,
                    mapNew));
        }

        if (node is StructuredConstructConditionalNode conditional)
        {
            return new TypeMapperControlFlowNode(
                Locals: [],
                Condition: conditional.Condition,
                WhenTrue: BuildRuntimeNode(
                    conditional.WhenTrue,
                    mapping,
                    memberMappings,
                    mapNew),
                WhenFalse: BuildRuntimeNode(
                    conditional.WhenFalse,
                    mapping,
                    memberMappings,
                    mapNew),
                Leaf: null,
                ThrowExpression: null);
        }

        var leaf = (StructuredConstructLeafNode)node;

        return leaf.Kind switch
        {
            StructuredConstructLeafKind.Constructor
                when leaf.Constructor is { } constructor =>
                BuildConstructorLeaf(mapping, constructor),
            StructuredConstructLeafKind.Factory
                when leaf.Factory is { } factory =>
                BuildFactoryLeaf(
                    mapping,
                    memberMappings,
                    factory),
            StructuredConstructLeafKind.Previous =>
                BuildPreviousLeaf(
                    mapping,
                    memberMappings,
                    mapNew),
            _ => BuildUnsupportedLeaf(
                mapping,
                mapNew,
                leaf.UnsupportedMessage ?? UnsupportedConstructMessage)
        };
    }

    private static TypeMapperControlFlowNode BuildFactoryLeaf(
        TypeMapperMappingModel mapping,
        ConventionMemberMappingPlan memberMappings,
        TypeMapperFactoryMappingModel factory)
    {
        var leaf = mapping with
        {
            MapNewFactory = factory,
            MapNewConstructor = null,
            MapNewMemberMappings = memberMappings.MapExisting,
            MapExistingMemberMappings = [],
            ControlFlow = null,
            MapNewUnsupportedExceptionMessage = null,
            MapExistingUnsupportedExceptionMessage = null,
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

    private static TypeMapperControlFlowNode BuildConstructorLeaf(
        TypeMapperMappingModel mapping,
        ConventionConstructorMappingPlan constructor)
    {
        var leaf = mapping with
        {
            MapNewConstructor = constructor.Constructor,
            MapNewMemberMappings =
                constructor.MapNewMemberMappings,
            MapExistingMemberMappings = [],
            ControlFlow = null,
            MapNewUnsupportedExceptionMessage = null,
            MapExistingUnsupportedExceptionMessage = null,
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
        bool mapNew)
    {
        if (mapNew)
        {
            return BuildUnsupportedLeaf(
                mapping,
                mapNew: true,
                UnavailablePreviousMessage);
        }

        var leaf = mapping with
        {
            MapNewConstructor = null,
            MapNewMemberMappings = [],
            MapExistingMemberMappings = memberMappings.MapExisting,
            ControlFlow = null,
            MapNewUnsupportedExceptionMessage = null,
            MapExistingUnsupportedExceptionMessage = null,
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
        bool mapNew,
        string message)
    {
        var leaf = mapping with
        {
            ControlFlow = null,
            MapNewUnsupportedExceptionMessage =
                mapNew ? message : null,
            MapExistingUnsupportedExceptionMessage =
                mapNew ? null : message,
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
    ExpressionSyntax Value);

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

internal sealed record StructuredConstructEvaluationNode(
    string Expression,
    StructuredConstructPlanNode Continuation)
    : StructuredConstructPlanNode;

internal sealed record StructuredConstructConditionalNode(
    string Condition,
    StructuredConstructPlanNode WhenTrue,
    StructuredConstructPlanNode WhenFalse)
    : StructuredConstructPlanNode;

internal sealed record StructuredConstructLeafNode(
    StructuredConstructLeafKind Kind,
    ConventionConstructorMappingPlan? Constructor,
    TypeMapperFactoryMappingModel? Factory,
    string? UnsupportedMessage)
    : StructuredConstructPlanNode
{
    public static StructuredConstructLeafNode Previous { get; } =
        new(
            StructuredConstructLeafKind.Previous,
            Constructor: null,
            Factory: null,
            UnsupportedMessage: null);

    public static StructuredConstructLeafNode Unsupported { get; } =
        new(
            StructuredConstructLeafKind.Unsupported,
            Constructor: null,
            Factory: null,
            UnsupportedConstructMappingMessage.Value);

    private static class UnsupportedConstructMappingMessage
    {
        public const string Value =
            "The configured structured Construct is not supported yet.";
    }
}

internal enum StructuredConstructLeafKind
{
    Constructor,
    Factory,
    Previous,
    Unsupported
}
