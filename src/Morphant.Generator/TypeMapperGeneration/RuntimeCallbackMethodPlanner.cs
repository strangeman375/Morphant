using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class RuntimeCallbackMethodPlanner
{
    public static RuntimeCallbackMethodPlan? Build(
        BoundConfigurationExpression configuration,
        bool hasPrevious,
        bool hasContext,
        INamedTypeSymbol mapperType,
        string helperMethodName,
        CancellationToken cancellationToken)
    {
        var syntax = configuration.Syntax;
        var semanticModel = configuration.SemanticModel;
        var delegateInvoke = configuration.DelegateInvokeMethod;
        var expectedParameterCount = 1 +
            (hasPrevious ? 1 : 0) +
            (hasContext ? 1 : 0);

        if (delegateInvoke.Parameters.Length != expectedParameterCount)
        {
            return null;
        }

        if (syntax is LambdaExpressionSyntax lambda)
        {
            return BuildLambda(
                lambda,
                configuration,
                hasPrevious,
                hasContext,
                mapperType,
                helperMethodName,
                cancellationToken);
        }

        return BuildDelegate(
            syntax,
            configuration,
            hasPrevious,
            hasContext,
            mapperType,
            helperMethodName,
            cancellationToken);
    }

    private static RuntimeCallbackMethodPlan? BuildLambda(
        LambdaExpressionSyntax lambda,
        BoundConfigurationExpression configuration,
        bool hasPrevious,
        bool hasContext,
        INamedTypeSymbol mapperType,
        string helperMethodName,
        CancellationToken cancellationToken)
    {
        var transferredSyntax =
            (CSharpSyntaxNode?)lambda.ExpressionBody ?? lambda.Block;

        if (transferredSyntax is null ||
            ContainsDslMarker(
                transferredSyntax,
                configuration.SemanticModel,
                cancellationToken) ||
            !TryGetLambdaParameters(
                lambda,
                configuration.SemanticModel,
                hasPrevious,
                hasContext,
                cancellationToken,
                out var sourceParameter,
                out var previousParameter,
                out var contextParameter))
        {
            return null;
        }

        var parameterNames = BuildParameterNames(
            transferredSyntax,
            sourceParameter,
            previousParameter,
            contextParameter);
        PreviousExpressionSubstitution? previousSubstitution =
            previousParameter is null
            ? null
            : new PreviousExpressionSubstitution(
                parameterNames.Previous!,
                parameterNames.Previous + ".Value",
                parameterNames.Previous + ".HasValue");

        if (!ConstructExpressionRewriter.TryRewriteSyntaxWithContext(
                transferredSyntax,
                configuration.SemanticModel,
                mapperType,
                sourceParameter,
                parameterNames.Source,
                previousParameter,
                previousSubstitution,
                resultParameter: null,
                resultName: null,
                contextParameter,
                parameterNames.Context,
                transferredSyntax,
                localSubstitutions: null,
                cancellationToken,
                out var rewrittenSyntax))
        {
            return null;
        }

        var helper = BuildMethodDeclaration(
            helperMethodName,
            configuration,
            mapperType,
            BuildParameters(
                configuration,
                mapperType,
                sourceParameter,
                previousParameter,
                contextParameter,
                parameterNames),
            lambda.Modifiers.Any(modifier =>
                modifier.IsKind(SyntaxKind.StaticKeyword)));

        helper = rewrittenSyntax switch
        {
            ExpressionSyntax expression => helper
                .WithExpressionBody(
                    SyntaxFactory.ArrowExpressionClause(expression))
                .WithSemicolonToken(
                    SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            BlockSyntax block => helper.WithBody(block),
            _ => helper
        };

        return new RuntimeCallbackMethodPlan(
            helperMethodName,
            NormalizeMethod(helper));
    }

    private static RuntimeCallbackMethodPlan? BuildDelegate(
        ExpressionSyntax expression,
        BoundConfigurationExpression configuration,
        bool hasPrevious,
        bool hasContext,
        INamedTypeSymbol mapperType,
        string helperMethodName,
        CancellationToken cancellationToken)
    {
        if (ContainsDslMarker(
                expression,
                configuration.SemanticModel,
                cancellationToken))
        {
            return null;
        }

        var parameters = configuration.DelegateInvokeMethod.Parameters;
        var sourceParameter = parameters[0];
        var previousParameter = hasPrevious ? parameters[1] : null;
        var contextParameter = hasContext
            ? parameters[parameters.Length - 1]
            : null;
        var parameterNames = BuildParameterNames(
            expression,
            sourceParameter,
            previousParameter,
            contextParameter);

        if (!ConstructExpressionRewriter.TryRewriteWithContext(
                expression,
                configuration.SemanticModel,
                mapperType,
                sourceParameter,
                parameterNames.Source,
                previousParameter,
                previousSubstitution: null,
                resultParameter: null,
                resultName: null,
                contextParameter,
                parameterNames.Context,
                expression,
                localSubstitutions: null,
                cancellationToken,
                out var rewrittenExpression))
        {
            return null;
        }

        var usedNames = new HashSet<string>(
            expression.DescendantTokens()
                .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
                .Select(token => token.ValueText),
            StringComparer.Ordinal)
        {
            parameterNames.Source
        };

        if (parameterNames.Previous is not null)
        {
            usedNames.Add(parameterNames.Previous);
        }

        if (parameterNames.Context is not null)
        {
            usedNames.Add(parameterNames.Context);
        }

        var delegateLocalName = UserResultMappingPlanner.AllocateName(
            "callback",
            usedNames);
        var substitutions = BuildTypeSubstitutions(
            configuration.SemanticModel,
            mapperType);
        var delegateType = MapperTypeSubstitution.Substitute(
            configuration.DelegateType,
            substitutions,
            configuration.SemanticModel.Compilation);
        var declaration = SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.ParseTypeName(
                        TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                            delegateType)))
                .WithVariables(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator(
                                SyntaxFactory.Identifier(delegateLocalName))
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.ParseExpression(
                                        rewrittenExpression))))));
        var invocationArguments = new List<ArgumentSyntax>
        {
            SyntaxFactory.Argument(
                SyntaxFactory.IdentifierName(parameterNames.Source))
        };

        if (parameterNames.Previous is { } previousName)
        {
            invocationArguments.Add(
                SyntaxFactory.Argument(
                    SyntaxFactory.IdentifierName(previousName)));
        }

        if (parameterNames.Context is { } contextName)
        {
            invocationArguments.Add(
                SyntaxFactory.Argument(
                    SyntaxFactory.IdentifierName(contextName)));
        }

        var invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.IdentifierName(delegateLocalName),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(invocationArguments)));
        var helper = BuildMethodDeclaration(
                helperMethodName,
                configuration,
                mapperType,
                BuildParameters(
                    configuration,
                    mapperType,
                    sourceParameter,
                    previousParameter,
                    contextParameter,
                    parameterNames),
                isStatic: false)
            .WithBody(
                SyntaxFactory.Block(
                    declaration,
                    SyntaxFactory.ReturnStatement(invocation)));

        return new RuntimeCallbackMethodPlan(
            helperMethodName,
            NormalizeMethod(helper));
    }

    private static MethodDeclarationSyntax BuildMethodDeclaration(
        string helperMethodName,
        BoundConfigurationExpression configuration,
        INamedTypeSymbol mapperType,
        SeparatedSyntaxList<ParameterSyntax> parameters,
        bool isStatic)
    {
        var substitutions = BuildTypeSubstitutions(
            configuration.SemanticModel,
            mapperType);
        var returnType = MapperTypeSubstitution.Substitute(
            configuration.DelegateInvokeMethod.ReturnType,
            substitutions,
            configuration.SemanticModel.Compilation);
        var modifiers = isStatic
            ? SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword))
            : SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName(
                    TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                        returnType)),
                SyntaxFactory.Identifier(helperMethodName))
            .WithModifiers(modifiers)
            .WithParameterList(
                SyntaxFactory.ParameterList(parameters));
    }

    private static SeparatedSyntaxList<ParameterSyntax> BuildParameters(
        BoundConfigurationExpression configuration,
        INamedTypeSymbol mapperType,
        IParameterSymbol source,
        IParameterSymbol? previous,
        IParameterSymbol? context,
        RuntimeCallbackParameterNames names)
    {
        var substitutions = BuildTypeSubstitutions(
            configuration.SemanticModel,
            mapperType);
        var parameters = new List<ParameterSyntax>
        {
            BuildParameter(
                names.Source,
                source,
                substitutions,
                configuration.SemanticModel.Compilation)
        };

        if (previous is not null)
        {
            parameters.Add(
                BuildParameter(
                    names.Previous!,
                    previous,
                    substitutions,
                    configuration.SemanticModel.Compilation));
        }

        if (context is not null)
        {
            parameters.Add(
                BuildParameter(
                    names.Context!,
                    context,
                    substitutions,
                    configuration.SemanticModel.Compilation));
        }

        return SyntaxFactory.SeparatedList(parameters);
    }

    private static ParameterSyntax BuildParameter(
        string name,
        IParameterSymbol parameter,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol> substitutions,
        Compilation compilation)
    {
        var type = MapperTypeSubstitution.Substitute(
            parameter.Type.WithNullableAnnotation(
                parameter.NullableAnnotation),
            substitutions,
            compilation);

        return SyntaxFactory.Parameter(SyntaxFactory.Identifier(name))
            .WithType(
                SyntaxFactory.ParseTypeName(
                    TypeMapperMappingTypePolicy.GetGeneratedTypeName(type)));
    }

    private static RuntimeCallbackParameterNames BuildParameterNames(
        CSharpSyntaxNode transferredSyntax,
        IParameterSymbol source,
        IParameterSymbol? previous,
        IParameterSymbol? context)
    {
        var usedNames = new HashSet<string>(
            transferredSyntax.DescendantTokens()
                .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
                .Select(token => token.ValueText),
            StringComparer.Ordinal);

        foreach (var parameter in new[] { source, previous, context })
        {
            if (parameter is not null && parameter.Name != "_")
            {
                usedNames.Remove(parameter.Name);
            }
        }

        return new RuntimeCallbackParameterNames(
            AllocateParameterName(source, "source", usedNames),
            previous is null
                ? null
                : AllocateParameterName(previous, "previous", usedNames),
            context is null
                ? null
                : AllocateParameterName(context, "context", usedNames));
    }

    private static string AllocateParameterName(
        IParameterSymbol parameter,
        string fallback,
        HashSet<string> usedNames)
    {
        if (parameter.Name != "_" && usedNames.Add(parameter.Name))
        {
            return parameter.Name;
        }

        return UserResultMappingPlanner.AllocateName(fallback, usedNames);
    }

    private static bool TryGetLambdaParameters(
        LambdaExpressionSyntax lambda,
        SemanticModel semanticModel,
        bool hasPrevious,
        bool hasContext,
        CancellationToken cancellationToken,
        out IParameterSymbol source,
        out IParameterSymbol? previous,
        out IParameterSymbol? context)
    {
        var parameters = lambda switch
        {
            SimpleLambdaExpressionSyntax simple =>
                new[] { simple.Parameter },
            ParenthesizedLambdaExpressionSyntax parenthesized =>
                parenthesized.ParameterList.Parameters.ToArray(),
            _ => []
        };
        var expectedCount = 1 +
            (hasPrevious ? 1 : 0) +
            (hasContext ? 1 : 0);

        if (parameters.Length != expectedCount ||
            semanticModel.GetDeclaredSymbol(
                parameters[0],
                cancellationToken) is not IParameterSymbol sourceParameter)
        {
            source = null!;
            previous = null;
            context = null;
            return false;
        }

        source = sourceParameter;
        var index = 1;
        previous = hasPrevious
            ? semanticModel.GetDeclaredSymbol(
                parameters[index++],
                cancellationToken) as IParameterSymbol
            : null;
        context = hasContext
            ? semanticModel.GetDeclaredSymbol(
                parameters[index],
                cancellationToken) as IParameterSymbol
            : null;

        return (!hasPrevious || previous is not null) &&
               (!hasContext || context is not null);
    }

    private static bool ContainsDslMarker(
        CSharpSyntaxNode syntax,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return DeclarativeIntrinsic.Contains(
            syntax,
            semanticModel,
            cancellationToken);
    }

    private static IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol>
        BuildTypeSubstitutions(
            SemanticModel semanticModel,
            INamedTypeSymbol mapperType)
    {
        var semanticMapperType = semanticModel.Compilation
                .GetTypeByMetadataName(
                    SymbolNameHelper.GetFullMetadataName(mapperType)) ??
            mapperType;

        return MapperTypeSubstitution.BuildForHierarchy(semanticMapperType);
    }

    private static string NormalizeMethod(MethodDeclarationSyntax method)
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

internal readonly record struct RuntimeCallbackMethodPlan(
    string HelperMethodName,
    string HelperMethodDeclaration);

internal readonly record struct RuntimeCallbackParameterNames(
    string Source,
    string? Previous,
    string? Context);
