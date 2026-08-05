using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ByFactoryMappingPlanner
{
    private const string ByFactoryMarkerMetadataName =
        "Morphant.Markers.IByFactoryMarker`1";

    private const string FuncMetadataName =
        "System.Func`1";

    private const string TypeMapperMetadataName =
        "Morphant.TypeMapper";

    private const string UnsupportedFactoryMessage =
        "The configured ByFactory construction is not supported yet.";

    private const string CreationOnlyMembersMessage =
        "The configured Members plan contains a creation-only rule that " +
        "cannot be applied to a factory result.";

    public static bool TryBuild(
        ImmutableArray<StructuredObjectArgument> arguments,
        TypeMapperMappingModel mapping,
        ImmutableArray<TypeMapperMemberMappingModel> memberMappings,
        bool hasExplicitCreationOnlyMappings,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        SyntaxNode transferScope,
        ByFactoryHelperRegistry helperRegistry,
        CancellationToken cancellationToken,
        out TypeMapperFactoryMappingModel? factory,
        out string? unsupportedMessage)
    {
        factory = null;
        unsupportedMessage = null;

        if (!arguments.Any(argument =>
                IsMarker(
                    argument.Value,
                    semanticModel,
                    cancellationToken)))
        {
            return false;
        }

        if (arguments.Length != 1)
        {
            unsupportedMessage = UnsupportedFactoryMessage;
            return true;
        }

        var markerArgument = arguments[0];

        if (markerArgument.Syntax.NameColon is
                { Name.Identifier.ValueText: not "marker" } ||
            !IsMarker(
                markerArgument.Value,
                semanticModel,
                cancellationToken) ||
            UnwrapParentheses(markerArgument.Value) is not
                InvocationExpressionSyntax markerInvocation ||
            !IsByFactoryInvocation(
                markerInvocation,
                semanticModel,
                cancellationToken) ||
            markerInvocation.ArgumentList.Arguments.Count != 1)
        {
            unsupportedMessage = UnsupportedFactoryMessage;
            return true;
        }

        var factoryArgument = markerInvocation.ArgumentList.Arguments[0];

        if (factoryArgument.NameColon is
                { Name.Identifier.ValueText: not "factory" } ||
            semanticModel.GetTypeInfo(
                    factoryArgument.Expression,
                    cancellationToken)
                .ConvertedType is not INamedTypeSymbol convertedType ||
            !StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    convertedType.OriginalDefinition),
                FuncMetadataName))
        {
            unsupportedMessage = UnsupportedFactoryMessage;
            return true;
        }

        var factoryExpression =
            UnwrapParentheses(factoryArgument.Expression);

        if (hasExplicitCreationOnlyMappings)
        {
            unsupportedMessage = CreationOnlyMembersMessage;
            return true;
        }

        TransferredFunctionPlan? BuildHelper(string functionName)
        {
            var returnType = convertedType.TypeArguments[0]
                .WithNullableAnnotation(
                    convertedType.TypeArgumentNullableAnnotations[0]);

            if (factoryExpression is
                    ParenthesizedLambdaExpressionSyntax
                    {
                        ParameterList.Parameters.Count: 0
                    } factoryLambda)
            {
                return UserResultMappingPlanner
                    .TryBuildTransferredFunction(
                        factoryLambda,
                        returnType,
                        sourceParameter,
                        previousParameter,
                        semanticModel,
                        mapperType,
                        functionName,
                        mapping.NonNullSourceName,
                        previousSubstitution?.OptionExpression,
                        cancellationToken,
                        out var lambdaFunction)
                        ? lambdaFunction
                        : null;
            }

            var usedLocalNames =
                UserResultMappingPlanner.BuildUsedLocalNames(mapperType);
            UserResultMappingPlanner.AddIdentifiers(
                factoryArgument.Expression,
                usedLocalNames);
            usedLocalNames.Add(sourceParameter.Name);

            if (previousParameter is not null)
            {
                usedLocalNames.Add(previousParameter.Name);
            }

            var delegateName =
                UserResultMappingPlanner.AllocateName(
                    "factory",
                    usedLocalNames);

            return UserResultMappingPlanner
                .TryBuildTransferredDelegateFunction(
                    factoryArgument.Expression,
                    convertedType,
                    invocationParameters: [],
                    sourceParameter,
                    previousParameter,
                    semanticModel,
                    mapperType,
                    functionName,
                    mapping.NonNullSourceName,
                    previousSubstitution?.OptionExpression,
                    transferScope,
                    cancellationToken,
                    delegateName,
                    out var delegateFunction)
                    ? delegateFunction
                    : null;
        }

        if (!helperRegistry.TryBuild(
                factoryArgument.Expression,
                BuildHelper,
                out var function))
        {
            unsupportedMessage = UnsupportedFactoryMessage;
            return true;
        }

        factory = UserResultMappingPlanner.BuildFactoryMapping(
            mapping,
            memberMappings,
            mapperType,
            function.ValueExpression);
        return true;
    }

    private static bool IsMarker(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        expression = UnwrapParentheses(expression);
        var typeInfo = semanticModel.GetTypeInfo(
            expression,
            cancellationToken);

        return IsMarkerType(typeInfo.Type) ||
               IsMarkerType(typeInfo.ConvertedType) ||
               semanticModel.GetSymbolInfo(
                       expression,
                       cancellationToken)
                   .Symbol is IMethodSymbol method &&
               IsMarkerType(method.ReturnType);
    }

    private static bool IsMarkerType(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol namedType &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       namedType.OriginalDefinition),
                   ByFactoryMarkerMetadataName);
    }

    private static bool IsByFactoryInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return semanticModel.GetSymbolInfo(
                    invocation,
                    cancellationToken)
                .Symbol is IMethodSymbol
                {
                    Name: "ByFactory",
                    ContainingType: { } containingType
                } &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(containingType),
                   TypeMapperMetadataName);
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

internal sealed class ByFactoryHelperRegistry
{
    private readonly Dictionary<int, HelperEntry> _entries = [];
    private readonly ImmutableArray<string>.Builder _declarations =
        ImmutableArray.CreateBuilder<string>();
    private readonly HashSet<string> _usedGeneratedMethodNames;
    private readonly HashSet<string> _allocatedNames =
        new(StringComparer.Ordinal);

    public ByFactoryHelperRegistry(
        HashSet<string> usedGeneratedMethodNames)
    {
        _usedGeneratedMethodNames = usedGeneratedMethodNames;
    }

    public ImmutableArray<string> HelperMethodDeclarations =>
        _declarations.ToImmutable();

    public bool TryBuild(
        ExpressionSyntax expression,
        Func<string, TransferredFunctionPlan?> build,
        out TransferredFunctionPlan function)
    {
        var key = expression.SpanStart;

        if (_entries.TryGetValue(key, out var existing))
        {
            if (build(existing.Name) is not { } candidate ||
                !StringComparer.Ordinal.Equals(
                    candidate.Declaration,
                    existing.Declaration))
            {
                function = default;
                return false;
            }

            function = candidate;
            return true;
        }

        var name = UserResultMappingPlanner.AllocateName(
            "CreateByFactory",
            _usedGeneratedMethodNames);

        if (build(name) is not { } created)
        {
            _usedGeneratedMethodNames.Remove(name);
            function = default;
            return false;
        }

        _allocatedNames.Add(name);
        _entries.Add(
            key,
            new HelperEntry(name, created.Declaration));
        _declarations.Add("private " + created.Declaration);
        function = created;
        return true;
    }

    public void Rollback()
    {
        foreach (var name in _allocatedNames)
        {
            _usedGeneratedMethodNames.Remove(name);
        }

        _allocatedNames.Clear();
        _entries.Clear();
        _declarations.Clear();
    }

    private readonly record struct HelperEntry(
        string Name,
        string Declaration);
}
