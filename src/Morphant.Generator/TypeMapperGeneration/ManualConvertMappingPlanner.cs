using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ManualConvertMappingPlanner
{
    private const string UnsupportedConvertMessage =
        "The configured Convert is not supported.";

    public static ManualConvertMappingResult Build(
        ConvertConfigurationModel configuration,
        TypeMapperMappingModel mapping,
        INamedTypeSymbol mapperType,
        HashSet<string> usedGeneratedMethodNames,
        CancellationToken cancellationToken)
    {
        if (configuration.Expression.Syntax is not
                ParenthesizedLambdaExpressionSyntax lambda ||
            !TryGetParameters(
                lambda,
                configuration.Expression.SemanticModel,
                cancellationToken,
                out var sourceParameter,
                out var previousParameter,
                out var contextParameter))
        {
            return ManualConvertMappingResult.Unsupported(
                UnsupportedConvertMessage);
        }

        var transferredSyntax =
            (CSharpSyntaxNode?)lambda.ExpressionBody ?? lambda.Block;

        if (transferredSyntax is null ||
            ContainsDslMarker(
                transferredSyntax,
                configuration.Expression.SemanticModel,
                cancellationToken))
        {
            return ManualConvertMappingResult.Unsupported(
                UnsupportedConvertMessage);
        }

        var parameterNames = BuildParameterNames(
            transferredSyntax,
            sourceParameter,
            previousParameter,
            contextParameter);
        var previousSubstitution = new PreviousExpressionSubstitution(
            parameterNames.Previous,
            parameterNames.Previous + ".Value",
            parameterNames.Previous + ".HasValue");

        CSharpSyntaxNode rewrittenSyntax;

        if (transferredSyntax is ExpressionSyntax expression)
        {
            if (!ConstructExpressionRewriter.TryRewriteSyntax(
                    expression,
                    configuration.Expression.SemanticModel,
                    mapperType,
                    sourceParameter,
                    parameterNames.Source,
                    previousParameter,
                    previousSubstitution,
                    contextParameter,
                    parameterNames.Context,
                    transferredSyntax,
                    cancellationToken,
                    out ExpressionSyntax rewrittenExpression))
            {
                return ManualConvertMappingResult.Unsupported(
                    UnsupportedConvertMessage);
            }

            rewrittenSyntax = rewrittenExpression;
        }
        else if (transferredSyntax is BlockSyntax block)
        {
            if (!ConstructExpressionRewriter.TryRewriteSyntax(
                    block,
                    configuration.Expression.SemanticModel,
                    mapperType,
                    sourceParameter,
                    parameterNames.Source,
                    previousParameter,
                    previousSubstitution,
                    contextParameter,
                    parameterNames.Context,
                    transferredSyntax,
                    cancellationToken,
                    out BlockSyntax rewrittenBlock))
            {
                return ManualConvertMappingResult.Unsupported(
                    UnsupportedConvertMessage);
            }

            rewrittenSyntax = rewrittenBlock;
        }
        else
        {
            return ManualConvertMappingResult.Unsupported(
                UnsupportedConvertMessage);
        }

        var helperMethodName = UserResultMappingPlanner.AllocateName(
            "ConvertDestination",
            usedGeneratedMethodNames);
        var helper = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName(
                    mapping.DestinationTypeName),
                SyntaxFactory.Identifier(helperMethodName))
            .WithModifiers(
                lambda.Modifiers.Any(modifier =>
                    modifier.IsKind(SyntaxKind.StaticKeyword))
                    ? SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                    : SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SeparatedList(
                        new[]
                        {
                            BuildParameter(
                                parameterNames.Source,
                                sourceParameter),
                            BuildParameter(
                                parameterNames.Previous,
                                previousParameter),
                            BuildParameter(
                                parameterNames.Context,
                                contextParameter)
                        })));

        helper = rewrittenSyntax switch
        {
            ExpressionSyntax rewrittenExpression => helper
                .WithExpressionBody(
                    SyntaxFactory.ArrowExpressionClause(
                        rewrittenExpression))
                .WithSemicolonToken(
                    SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            BlockSyntax rewrittenBlock => helper.WithBody(rewrittenBlock),
            _ => helper
        };

        return new ManualConvertMappingResult(
            helperMethodName,
            NormalizeMethod(helper),
            UnsupportedMessage: null);
    }

    private static bool TryGetParameters(
        ParenthesizedLambdaExpressionSyntax lambda,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out IParameterSymbol source,
        out IParameterSymbol previous,
        out IParameterSymbol context)
    {
        if (lambda.ParameterList.Parameters.Count != 3 ||
            semanticModel.GetDeclaredSymbol(
                    lambda.ParameterList.Parameters[0],
                    cancellationToken) is not IParameterSymbol sourceValue ||
            semanticModel.GetDeclaredSymbol(
                    lambda.ParameterList.Parameters[1],
                    cancellationToken) is not IParameterSymbol previousValue ||
            semanticModel.GetDeclaredSymbol(
                    lambda.ParameterList.Parameters[2],
                    cancellationToken) is not IParameterSymbol contextValue)
        {
            source = null!;
            previous = null!;
            context = null!;
            return false;
        }

        source = sourceValue;
        previous = previousValue;
        context = contextValue;
        return true;
    }

    private static ManualConvertParameterNames BuildParameterNames(
        CSharpSyntaxNode transferredSyntax,
        IParameterSymbol source,
        IParameterSymbol previous,
        IParameterSymbol context)
    {
        var usedNames = new HashSet<string>(
            transferredSyntax
                .DescendantTokens()
                .Where(token =>
                    token.IsKind(SyntaxKind.IdentifierToken))
                .Select(token => token.ValueText),
            StringComparer.Ordinal);

        foreach (var parameter in new[] { source, previous, context })
        {
            if (parameter.Name != "_")
            {
                usedNames.Remove(parameter.Name);
            }
        }

        return new ManualConvertParameterNames(
            AllocateParameterName(source, "source", usedNames),
            AllocateParameterName(previous, "previous", usedNames),
            AllocateParameterName(context, "context", usedNames));
    }

    private static string AllocateParameterName(
        IParameterSymbol parameter,
        string fallback,
        HashSet<string> usedNames)
    {
        if (parameter.Name != "_")
        {
            usedNames.Add(parameter.Name);
            return parameter.Name;
        }

        return UserResultMappingPlanner.AllocateName(
            fallback,
            usedNames);
    }

    private static ParameterSyntax BuildParameter(
        string name,
        IParameterSymbol parameter)
    {
        return SyntaxFactory.Parameter(
                SyntaxFactory.Identifier(name))
            .WithType(
                SyntaxFactory.ParseTypeName(
                    TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                        parameter.Type.WithNullableAnnotation(
                            parameter.NullableAnnotation))));
    }

    private static bool ContainsDslMarker(
        CSharpSyntaxNode syntax,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var invocation in syntax
                     .DescendantNodesAndSelf()
                     .OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (semanticModel.GetSymbolInfo(
                    invocation,
                    cancellationToken).Symbol is not IMethodSymbol method)
            {
                continue;
            }

            method = method.ReducedFrom ?? method;

            if (method.Name is
                    "Auto" or
                    "Ignore" or
                    "Map" or
                    "ByConvention" or
                    "ByFactory" &&
                StringComparer.Ordinal.Equals(
                    SymbolNameHelper.GetFullMetadataName(
                        method.ContainingType),
                    MetadataNames.TypeMapper))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeMethod(
        MethodDeclarationSyntax method)
    {
        return new NullableSuppressionTriviaRewriter()
            .Visit(
                method
                    .WithoutTrivia()
                    .NormalizeWhitespace(
                        indentation: "    ",
                        eol: "\r\n"))!
            .ToFullString();
    }

    private sealed class NullableSuppressionTriviaRewriter :
        CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitPostfixUnaryExpression(
            PostfixUnaryExpressionSyntax node)
        {
            var rewritten =
                (PostfixUnaryExpressionSyntax)
                base.VisitPostfixUnaryExpression(node)!;

            if (!rewritten.IsKind(
                    SyntaxKind.SuppressNullableWarningExpression))
            {
                return rewritten;
            }

            return rewritten
                .WithOperand(
                    rewritten.Operand.WithTrailingTrivia(
                        default(SyntaxTriviaList)))
                .WithOperatorToken(
                    rewritten.OperatorToken.WithLeadingTrivia(
                        default(SyntaxTriviaList)));
        }
    }
}

internal readonly record struct ManualConvertMappingResult
(
    string? HelperMethodName,
    string? HelperMethodDeclaration,
    string? UnsupportedMessage
)
{
    public static ManualConvertMappingResult Unsupported(string message) =>
        new(
            HelperMethodName: null,
            HelperMethodDeclaration: null,
            UnsupportedMessage: message);
}

internal readonly record struct ManualConvertParameterNames
(
    string Source,
    string Previous,
    string Context
);
