using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class UserResultMappingPlanner
{
    public static bool TryBuildTransferredFunction(
        LambdaExpressionSyntax lambda,
        ITypeSymbol returnType,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        string functionName,
        string sourceInvocationExpression,
        string? previousInvocationExpression,
        CancellationToken cancellationToken,
        out TransferredFunctionPlan plan)
    {
        var transferredSyntax =
            (CSharpSyntaxNode?)lambda.ExpressionBody ??
            lambda.Block;

        if (transferredSyntax is null)
        {
            plan = default;
            return false;
        }

        var allowedCaptures = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default)
        {
            sourceParameter
        };

        if (previousParameter is not null)
        {
            allowedCaptures.Add(previousParameter);
        }

        if (!TransferableLambdaSyntax.TryGetCaptures(
                transferredSyntax,
                semanticModel,
                allowedCaptures,
                cancellationToken,
                out var captures))
        {
            plan = default;
            return false;
        }

        captures = captures
            .OrderBy(capture =>
                SymbolEqualityComparer.Default.Equals(
                    capture,
                    sourceParameter)
                    ? 0
                    : 1)
            .ToImmutableArray();
        var sourceParameterName = Identifier(sourceParameter.Name);
        var previousParameterName = previousParameter is null
            ? null
            : Identifier(previousParameter.Name);
        PreviousExpressionSubstitution? previousSubstitution =
            previousParameterName is null
            ? null
            : new PreviousExpressionSubstitution(
                previousParameterName,
                previousParameterName + ".Value",
                previousParameterName + ".HasValue");

        if (!ConstructExpressionRewriter.TryRewriteSyntax(
                transferredSyntax,
                semanticModel,
                mapperType,
                sourceParameter,
                sourceParameterName,
                previousParameter,
                previousSubstitution,
                transferredSyntax,
                cancellationToken,
                out var rewrittenSyntax))
        {
            plan = default;
            return false;
        }

        var parameters = ImmutableArray.CreateBuilder<ParameterSyntax>();
        var invocationArguments = ImmutableArray.CreateBuilder<string>();

        foreach (var capture in captures)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    capture,
                    sourceParameter))
            {
                parameters.Add(
                    BuildParameter(
                        sourceParameterName,
                        sourceParameter.Type.WithNullableAnnotation(
                            sourceParameter.NullableAnnotation)));
                invocationArguments.Add(sourceInvocationExpression);
                continue;
            }

            if (previousParameter is not null &&
                SymbolEqualityComparer.Default.Equals(
                    capture,
                    previousParameter) &&
                previousInvocationExpression is not null)
            {
                parameters.Add(
                    BuildParameter(
                        previousParameterName!,
                        previousParameter.Type.WithNullableAnnotation(
                            previousParameter.NullableAnnotation)));
                invocationArguments.Add(previousInvocationExpression);
                continue;
            }

            plan = default;
            return false;
        }

        var function = SyntaxFactory.LocalFunctionStatement(
                SyntaxFactory.ParseTypeName(
                    TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                        returnType)),
                SyntaxFactory.Identifier(functionName))
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SeparatedList(parameters)));

        if (lambda.Modifiers.Any(modifier =>
                modifier.IsKind(SyntaxKind.StaticKeyword)))
        {
            function = function.WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)));
        }

        function = rewrittenSyntax switch
        {
            ExpressionSyntax expression => function
                .WithExpressionBody(
                    SyntaxFactory.ArrowExpressionClause(expression))
                .WithSemicolonToken(
                    SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            BlockSyntax block => function.WithBody(block),
            _ => function
        };

        plan = new TransferredFunctionPlan(
            NormalizeFunction(function),
            functionName +
            "(" +
            string.Join(", ", invocationArguments) +
            ")");
        return true;
    }

    public static bool TryRewriteDelegate(
        ExpressionSyntax expression,
        INamedTypeSymbol delegateType,
        ImmutableArray<string> invocationArguments,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        SyntaxNode transferScope,
        CancellationToken cancellationToken,
        string localName,
        out TypeMapperFactoryDelegateModel factoryDelegate,
        out string valueExpression)
    {
        if (!ConstructExpressionRewriter.TryRewrite(
                expression,
                semanticModel,
                mapperType,
                sourceParameter,
                sourceParameter.Name,
                previousParameter,
                previousSubstitution,
                transferScope,
                cancellationToken,
                out var rewrittenExpression))
        {
            factoryDelegate = default;
            valueExpression = string.Empty;
            return false;
        }

        factoryDelegate = new TypeMapperFactoryDelegateModel(
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(delegateType),
            localName,
            rewrittenExpression);
        valueExpression =
            localName +
            "(" +
            string.Join(", ", invocationArguments) +
            ")";
        return true;
    }

    public static TypeMapperFactoryMappingModel BuildFactoryMapping(
        TypeMapperMappingModel mapping,
        ImmutableArray<TypeMapperMemberMappingModel> memberMappings,
        INamedTypeSymbol mapperType,
        string valueExpression,
        string? localFunctionName,
        string? localFunctionDeclaration,
        TypeMapperFactoryDelegateModel? factoryDelegate)
    {
        var usedNames = BuildUsedLocalNames(mapperType);
        usedNames.Add(mapping.NonNullSourceName);

        if (localFunctionDeclaration is not null)
        {
            AddIdentifiers(localFunctionDeclaration, usedNames);
        }

        if (factoryDelegate is { } delegateValue)
        {
            usedNames.Add(delegateValue.LocalName);
            AddIdentifiers(delegateValue.ValueExpression, usedNames);
        }

        AddIdentifiers(valueExpression, usedNames);

        var resultName = AllocateName("result", usedNames);
        var nullableValueName =
            mapping.MapExistingKind ==
                TypeMapperMapExistingKind.NullableValue &&
            !memberMappings.IsEmpty
                ? AllocateName("resultValue", usedNames)
                : null;

        return new TypeMapperFactoryMappingModel(
            localFunctionName,
            localFunctionDeclaration,
            valueExpression,
            factoryDelegate,
            RuntimeLocalDependencies: [],
            resultName,
            nullableValueName,
            DestinationRequiresNullForgivingOperator: false,
            RequiresNullGuard:
                mapping.DestinationCanBeNull &&
                !memberMappings.IsEmpty);
    }

    public static HashSet<string> BuildUsedLocalNames(
        INamedTypeSymbol mapperType)
    {
        var result = new HashSet<string>(StringComparer.Ordinal)
        {
            "source",
            "destination",
            "context"
        };

        for (var type = mapperType;
             type is not null;
             type = type.ContainingType)
        {
            result.Add(type.Name);

            foreach (var typeParameter in type.TypeParameters)
            {
                result.Add(typeParameter.Name);
            }
        }

        for (var type = mapperType;
             type is not null;
             type = type.BaseType)
        {
            foreach (var member in type.GetMembers())
            {
                result.Add(member.Name);
            }
        }

        return result;
    }

    public static string AllocateName(
        string preferredName,
        HashSet<string> usedNames)
    {
        if (usedNames.Add(preferredName))
        {
            return preferredName;
        }

        for (var suffix = 1;; suffix++)
        {
            var candidate = preferredName +
                suffix.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);

            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }

    public static void AddIdentifiers(
        SyntaxNode syntax,
        HashSet<string> names)
    {
        foreach (var token in syntax.DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.IdentifierToken))
            {
                names.Add(token.ValueText);
            }
        }
    }

    private static ParameterSyntax BuildParameter(
        string name,
        ITypeSymbol type)
    {
        return SyntaxFactory.Parameter(
                SyntaxFactory.Identifier(name))
            .WithType(
                SyntaxFactory.ParseTypeName(
                    TypeMapperMappingTypePolicy.GetGeneratedTypeName(type)));
    }

    private static string NormalizeFunction(
        LocalFunctionStatementSyntax function)
    {
        return new NullableSuppressionTriviaRewriter()
            .Visit(
                function
                    .WithoutTrivia()
                    .NormalizeWhitespace(
                        indentation: "    ",
                        eol: "\r\n"))!
            .ToFullString();
    }

    private static void AddIdentifiers(
        string syntax,
        HashSet<string> names)
    {
        foreach (var token in SyntaxFactory.ParseStatement(syntax)
                     .DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.IdentifierToken))
            {
                names.Add(token.ValueText);
            }
        }
    }

    private static string Identifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value
            : value;
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

internal readonly record struct TransferredFunctionPlan(
    string Declaration,
    string ValueExpression);
