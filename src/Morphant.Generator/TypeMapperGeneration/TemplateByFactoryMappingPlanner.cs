using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TemplateByFactoryMappingPlanner
{
    private const string ByFactoryMarkerMetadataName =
        "Morphant.Markers.IByFactoryMarker`1";

    private const string FuncMetadataName =
        "System.Func`1";

    private const string TypeMapperMetadataName =
        "Morphant.TypeMapper";

    private const string UnsupportedCaptureMessage =
        "ByFactory contains a capture that cannot be transferred " +
        "to the generated mapper.";

    public static bool TryBuild(
        ImmutableArray<TemplateObjectArgumentSyntax> arguments,
        ITypeSymbol factoryDestinationType,
        SemanticModel semanticModel,
        HashSet<ISymbol> allowedCapturedSymbols,
        CancellationToken cancellationToken,
        out TemplateFactorySyntaxPlan? factory)
    {
        factory = null;

        if (!ContainsMarker(
                arguments,
                semanticModel,
                cancellationToken))
        {
            return false;
        }

        if (arguments.Length != 1)
        {
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
            return true;
        }

        var factoryArgument =
            markerInvocation.ArgumentList.Arguments[0];

        if (factoryArgument.NameColon is
                { Name.Identifier.ValueText: not "factory" })
        {
            return true;
        }

        if (semanticModel.GetTypeInfo(
                    factoryArgument.Expression,
                    cancellationToken)
                .ConvertedType is not
                INamedTypeSymbol convertedType ||
            !StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    convertedType.OriginalDefinition),
                FuncMetadataName))
        {
            return true;
        }

        var factoryExpression =
            UnwrapParentheses(factoryArgument.Expression);
        var transferredSyntax =
            factoryExpression is
                ParenthesizedLambdaExpressionSyntax
                {
                    ParameterList.Parameters.Count: 0
                } lambda
                ? (SyntaxNode)lambda
                : factoryArgument.Expression;

        if (!TransferableLambdaSyntax.TryGetCaptures(
                transferredSyntax,
                semanticModel,
                allowedCapturedSymbols,
                "__morphantFactoryCapture",
                cancellationToken,
                out var captures))
        {
            factory = TemplateFactorySyntaxPlan.Unsupported(
                UnsupportedCaptureMessage);
            return true;
        }

        var convertedReturnType =
            convertedType.TypeArguments[0]
                .WithNullableAnnotation(
                    convertedType.TypeArgumentNullableAnnotations[0]);
        var returnType =
            convertedReturnType.TypeKind == TypeKind.Error
                ? factoryDestinationType
                : convertedReturnType;
        var returnTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                returnType);
        var convertedTypeName =
            convertedReturnType.TypeKind == TypeKind.Error
                ? "global::System.Func<" +
                  returnTypeName +
                  ">"
                : TypeMapperMappingTypePolicy
                    .GetGeneratedTypeName(
                        convertedType);

        if (factoryExpression is
            ParenthesizedLambdaExpressionSyntax
            {
                ParameterList.Parameters.Count: 0
            } factoryLambda)
        {
            factory = new TemplateFactorySyntaxPlan(
                returnTypeName,
                ConvertedTypeName: null,
                factoryLambda.ExpressionBody,
                factoryLambda.Block,
                DelegateExpression: null,
                IsStatic:
                    factoryLambda.Modifiers.Any(
                        static modifier =>
                            modifier.IsKind(
                                SyntaxKind.StaticKeyword)),
                captures,
                UnsupportedMessage: null);
            return true;
        }

        factory = new TemplateFactorySyntaxPlan(
            returnTypeName,
            convertedTypeName,
            ExpressionBody: null,
            BlockBody: null,
            DelegateExpression:
                factoryArgument.Expression,
            IsStatic: false,
            captures,
            UnsupportedMessage: null);
        return true;
    }

    private static bool ContainsMarker(
        ImmutableArray<TemplateObjectArgumentSyntax> arguments,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsMarker(
                    argument.Value,
                    semanticModel,
                    cancellationToken))
            {
                return true;
            }
        }

        return false;
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
                   SymbolNameHelper.GetFullMetadataName(
                       containingType),
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

internal sealed record TemplateFactorySyntaxPlan(
    string ReturnTypeName,
    string? ConvertedTypeName,
    ExpressionSyntax? ExpressionBody,
    BlockSyntax? BlockBody,
    ExpressionSyntax? DelegateExpression,
    bool IsStatic,
    ImmutableArray<TransferableLambdaCaptureSyntax> Captures,
    string? UnsupportedMessage)
{
    public static TemplateFactorySyntaxPlan Unsupported(
        string message)
    {
        return new TemplateFactorySyntaxPlan(
            ReturnTypeName: string.Empty,
            ConvertedTypeName: null,
            ExpressionBody: null,
            BlockBody: null,
            DelegateExpression: null,
            IsStatic: false,
            Captures: [],
            UnsupportedMessage: message);
    }
}
