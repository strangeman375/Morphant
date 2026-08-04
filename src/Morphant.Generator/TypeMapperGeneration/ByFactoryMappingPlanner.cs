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

    public static bool TryBuild(
        ImmutableArray<StructuredObjectArgument> arguments,
        TypeMapperMappingModel mapping,
        ImmutableArray<TypeMapperMemberMappingModel> memberMappings,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        SyntaxNode transferScope,
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
        var usedNames =
            UserResultMappingPlanner.BuildUsedLocalNames(mapperType);
        UserResultMappingPlanner.AddIdentifiers(
            factoryArgument.Expression,
            usedNames);

        if (factoryExpression is
            ParenthesizedLambdaExpressionSyntax
            {
                ParameterList.Parameters.Count: 0
            } factoryLambda)
        {
            var functionName =
                UserResultMappingPlanner.AllocateName(
                    "CreateByFactory",
                    usedNames);
            var returnType = convertedType.TypeArguments[0]
                .WithNullableAnnotation(
                    convertedType.TypeArgumentNullableAnnotations[0]);

            if (!UserResultMappingPlanner.TryBuildTransferredFunction(
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
                    out var function))
            {
                unsupportedMessage = UnsupportedFactoryMessage;
                return true;
            }

            factory = UserResultMappingPlanner.BuildFactoryMapping(
                mapping,
                memberMappings,
                mapperType,
                function.ValueExpression,
                functionName,
                function.Declaration,
                factoryDelegate: null);
            return true;
        }

        var delegateName =
            UserResultMappingPlanner.AllocateName(
                "factory",
                usedNames);

        if (!UserResultMappingPlanner.TryRewriteDelegate(
                factoryArgument.Expression,
                convertedType,
                invocationArguments: [],
                semanticModel,
                mapperType,
                sourceParameter,
                previousParameter,
                previousSubstitution,
                transferScope,
                cancellationToken,
                delegateName,
                out var factoryDelegate,
                out var valueExpression))
        {
            unsupportedMessage = UnsupportedFactoryMessage;
            return true;
        }

        factory = UserResultMappingPlanner.BuildFactoryMapping(
            mapping,
            memberMappings,
            mapperType,
            valueExpression,
            localFunctionName: null,
            localFunctionDeclaration: null,
            factoryDelegate);
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
