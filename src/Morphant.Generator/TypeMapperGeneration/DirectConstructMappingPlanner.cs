using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DirectConstructMappingPlanner
{
    private const string UnsupportedConstructMessage =
        "The configured direct Construct is not supported yet.";

    public static DirectConstructMappingResult Build(
        ConstructConfigurationModel configuration,
        TypeMapperMappingModel mapping,
        ConventionMemberMappingPlan memberMappings,
        INamedTypeSymbol mapperType,
        HashSet<string> usedGeneratedMethodNames,
        CancellationToken cancellationToken)
    {
        var postConstructionMembers = memberMappings.MapExisting;
        var helperDeclarations = ImmutableArray<string>.Empty;
        TransferredFunctionPlan? blockFunction = null;
        TransferredFunctionPlan? delegateFunction = null;
        string? delegateLocalName = null;
        string? allocatedHelperName = null;
        IParameterSymbol? sourceParameter = null;
        IParameterSymbol? previousParameter = null;
        var syntax = configuration.Expression.Syntax;

        if (syntax is LambdaExpressionSyntax lambda)
        {
            if (!TryGetLambdaParameters(
                    lambda,
                    configuration.Expression.SemanticModel,
                    configuration.Form,
                    cancellationToken,
                    out sourceParameter,
                    out previousParameter))
            {
                return DirectConstructMappingResult.Unsupported(
                    UnsupportedConstructMessage);
            }

            if (lambda.Block is not null)
            {
                var helperName = AllocateGeneratedMethodName(
                    "ConstructDestination",
                    usedGeneratedMethodNames);
                allocatedHelperName = helperName;
                var initialPreviousExpression =
                    configuration.Form ==
                    ConstructConfigurationForm.SourceAndPrevious
                        ? BuildPreviousOptionExpression(
                            mapping,
                            hasPrevious: false)
                        : null;

                if (!UserResultMappingPlanner.TryBuildTransferredFunction(
                        lambda,
                        configuration.Expression.DelegateInvokeMethod
                            .ReturnType,
                        sourceParameter,
                        previousParameter,
                        configuration.Expression.SemanticModel,
                        mapperType,
                        helperName,
                        mapping.NonNullSourceName,
                        initialPreviousExpression,
                        cancellationToken,
                        out var function))
                {
                    usedGeneratedMethodNames.Remove(helperName);
                    allocatedHelperName = null;
                    return DirectConstructMappingResult.Unsupported(
                        UnsupportedConstructMessage);
                }

                blockFunction = function;
                helperDeclarations =
                    ["private " + function.Declaration];
            }
        }
        else
        {
            sourceParameter =
                configuration.Expression.DelegateInvokeMethod
                    .Parameters[0];
            previousParameter =
                configuration.Form ==
                    ConstructConfigurationForm.SourceAndPrevious
                    ? configuration.Expression.DelegateInvokeMethod
                        .Parameters[1]
                    : null;
            var helperName = AllocateGeneratedMethodName(
                "ConstructDestination",
                usedGeneratedMethodNames);
            allocatedHelperName = helperName;
            var usedLocalNames =
                UserResultMappingPlanner.BuildUsedLocalNames(mapperType);
            UserResultMappingPlanner.AddIdentifiers(
                syntax,
                usedLocalNames);
            usedLocalNames.Add(sourceParameter.Name);

            if (previousParameter is not null)
            {
                usedLocalNames.Add(previousParameter.Name);
            }

            delegateLocalName =
                UserResultMappingPlanner.AllocateName(
                    "construct",
                    usedLocalNames);
            var initialPreviousExpression =
                previousParameter is null
                    ? null
                    : BuildPreviousOptionExpression(
                        mapping,
                        hasPrevious: false);

            if (!UserResultMappingPlanner
                    .TryBuildTransferredDelegateFunction(
                        syntax,
                        configuration.Expression.DelegateType,
                        configuration.Expression.DelegateInvokeMethod
                            .Parameters,
                        sourceParameter,
                        previousParameter,
                        configuration.Expression.SemanticModel,
                        mapperType,
                        helperName,
                        mapping.NonNullSourceName,
                        initialPreviousExpression,
                        syntax,
                        cancellationToken,
                        delegateLocalName,
                        out var function))
            {
                usedGeneratedMethodNames.Remove(helperName);
                return DirectConstructMappingResult.Unsupported(
                    UnsupportedConstructMessage);
            }

            delegateFunction = function;
            helperDeclarations =
                ["private " + function.Declaration];
        }

        TypeMapperControlFlowNode? BuildUserResultLeaf(
            bool hasPrevious)
        {
            PreviousExpressionSubstitution? previousSubstitution =
                previousParameter is null
                ? null
                : BuildPreviousSubstitution(mapping, hasPrevious);
            string valueExpression;

            if (syntax is LambdaExpressionSyntax lambda)
            {
                if (lambda.ExpressionBody is { } expressionBody)
                {
                    if (!ConstructExpressionRewriter.TryRewrite(
                            expressionBody,
                            configuration.Expression.SemanticModel,
                            mapperType,
                            sourceParameter!,
                            mapping.NonNullSourceName,
                            previousParameter,
                            previousSubstitution,
                            expressionBody,
                            cancellationToken,
                            out valueExpression))
                    {
                        return null;
                    }
                }
                else if (blockFunction is { } function)
                {
                    valueExpression = BuildBlockInvocation(
                        function,
                        configuration,
                        mapping,
                        sourceParameter!,
                        previousParameter,
                        hasPrevious,
                        mapperType,
                        cancellationToken);

                    if (valueExpression.Length == 0)
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            else if (delegateFunction is { } initialDelegateFunction)
            {
                valueExpression = BuildDelegateInvocation(
                    initialDelegateFunction,
                    configuration,
                    mapping,
                    sourceParameter!,
                    previousParameter,
                    hasPrevious,
                    mapperType,
                    delegateLocalName!,
                    cancellationToken);

                if (valueExpression.Length == 0)
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

            var factory =
                UserResultMappingPlanner.BuildFactoryMapping(
                    mapping,
                    postConstructionMembers,
                    mapperType,
                    valueExpression);
            var leaf = mapping with
            {
                MapNewDirectExpression = null,
                MapExistingDirectExpression = null,
                MapNewFactory = factory,
                MapNewConstructor = null,
                MapNewMemberMappings = postConstructionMembers,
                MapExistingMemberMappings = [],
                ControlFlow = null,
                MapNewUnsupportedExceptionMessage = null,
                MapExistingUnsupportedExceptionMessage = null,
                UnsupportedExceptionMessage = null
            };

            return Leaf(leaf);
        }

        var mapNewRoot = BuildUserResultLeaf(hasPrevious: false);

        if (mapNewRoot is null)
        {
            if (allocatedHelperName is not null)
            {
                usedGeneratedMethodNames.Remove(allocatedHelperName);
            }

            return DirectConstructMappingResult.Unsupported(
                UnsupportedConstructMessage);
        }

        TypeMapperControlFlowNode? mapExistingRoot =
            configuration.Form == ConstructConfigurationForm.Source
                ? BuildPreviousLeaf(
                    mapping,
                    postConstructionMembers)
                : BuildUserResultLeaf(hasPrevious: true);

        if (mapExistingRoot is null)
        {
            if (allocatedHelperName is not null)
            {
                usedGeneratedMethodNames.Remove(allocatedHelperName);
            }

            return DirectConstructMappingResult.Unsupported(
                UnsupportedConstructMessage);
        }

        return new DirectConstructMappingResult(
            new TypeMapperControlFlowMappingModel(
                mapNewRoot,
                mapExistingRoot),
            helperDeclarations,
            UnsupportedMessage: null);
    }

    private static string BuildBlockInvocation(
        TransferredFunctionPlan initialFunction,
        ConstructConfigurationModel configuration,
        TypeMapperMappingModel mapping,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
        bool hasPrevious,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (configuration.Expression.Syntax is not
                LambdaExpressionSyntax lambda ||
            !UserResultMappingPlanner.TryBuildTransferredFunction(
                lambda,
                configuration.Expression.DelegateInvokeMethod.ReturnType,
                sourceParameter,
                previousParameter,
                configuration.Expression.SemanticModel,
                mapperType,
                GetFunctionName(initialFunction.Declaration),
                mapping.NonNullSourceName,
                previousParameter is null
                    ? null
                    : BuildPreviousOptionExpression(
                        mapping,
                        hasPrevious),
                cancellationToken,
                out var function) ||
            !StringComparer.Ordinal.Equals(
                function.Declaration,
                initialFunction.Declaration))
        {
            return string.Empty;
        }

        return function.ValueExpression;
    }

    private static string BuildDelegateInvocation(
        TransferredFunctionPlan initialFunction,
        ConstructConfigurationModel configuration,
        TypeMapperMappingModel mapping,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
        bool hasPrevious,
        INamedTypeSymbol mapperType,
        string delegateLocalName,
        CancellationToken cancellationToken)
    {
        if (!UserResultMappingPlanner
                .TryBuildTransferredDelegateFunction(
                    configuration.Expression.Syntax,
                    configuration.Expression.DelegateType,
                    configuration.Expression.DelegateInvokeMethod
                        .Parameters,
                    sourceParameter,
                    previousParameter,
                    configuration.Expression.SemanticModel,
                    mapperType,
                    GetFunctionName(initialFunction.Declaration),
                    mapping.NonNullSourceName,
                    previousParameter is null
                        ? null
                        : BuildPreviousOptionExpression(
                            mapping,
                            hasPrevious),
                    configuration.Expression.Syntax,
                    cancellationToken,
                    delegateLocalName,
                    out var function) ||
            !StringComparer.Ordinal.Equals(
                function.Declaration,
                initialFunction.Declaration))
        {
            return string.Empty;
        }

        return function.ValueExpression;
    }

    private static string GetFunctionName(string declaration)
    {
        var function = SyntaxFactory.ParseStatement(declaration) as
            LocalFunctionStatementSyntax;

        return function?.Identifier.ValueText ?? string.Empty;
    }

    private static PreviousExpressionSubstitution
        BuildPreviousSubstitution(
            TypeMapperMappingModel mapping,
            bool hasPrevious)
    {
        var optionExpression = BuildPreviousOptionExpression(
            mapping,
            hasPrevious);

        return hasPrevious
            ? new PreviousExpressionSubstitution(
                optionExpression,
                "destination",
                "true")
            : new PreviousExpressionSubstitution(
                optionExpression,
                optionExpression + ".Value",
                "false");
    }

    private static string BuildPreviousOptionExpression(
        TypeMapperMappingModel mapping,
        bool hasPrevious)
    {
        var optionTypeName =
            "global::Morphant.Option<" +
            mapping.NonNullDestinationTypeName +
            ">";

        return hasPrevious
            ? optionTypeName + ".Some(destination)"
            : optionTypeName + ".None";
    }

    private static TypeMapperControlFlowNode BuildPreviousLeaf(
        TypeMapperMappingModel mapping,
        ImmutableArray<TypeMapperMemberMappingModel> memberMappings)
    {
        var leaf = mapping with
        {
            MapNewFactory = null,
            MapNewConstructor = null,
            MapNewMemberMappings = [],
            MapExistingMemberMappings = memberMappings,
            ControlFlow = null,
            MapNewUnsupportedExceptionMessage = null,
            MapExistingUnsupportedExceptionMessage = null,
            UnsupportedExceptionMessage = null
        };

        return Leaf(leaf);
    }

    private static TypeMapperControlFlowNode Leaf(
        TypeMapperMappingModel mapping)
    {
        return new TypeMapperControlFlowNode(
            Locals: [],
            Condition: null,
            WhenTrue: null,
            WhenFalse: null,
            Leaf: mapping,
            ThrowExpression: null);
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
        previousParameter = expectedCount == 1
            ? null
            : semanticModel.GetDeclaredSymbol(
                    parameters[1],
                    cancellationToken) as IParameterSymbol;
        return expectedCount == 1 || previousParameter is not null;
    }

    private static string AllocateGeneratedMethodName(
        string preferredName,
        HashSet<string> usedNames)
    {
        return UserResultMappingPlanner.AllocateName(
            preferredName,
            usedNames);
    }
}

internal readonly record struct DirectConstructMappingResult(
    TypeMapperControlFlowMappingModel? ControlFlow,
    ImmutableArray<string> HelperMethodDeclarations,
    string? UnsupportedMessage)
{
    public static DirectConstructMappingResult Unsupported(
        string message) =>
        new(
            ControlFlow: null,
            HelperMethodDeclarations: [],
            UnsupportedMessage: message);
}
