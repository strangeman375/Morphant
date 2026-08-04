using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.PairConfiguration;

internal static class PairConfigurationDiscoveryPipeline
{
    public static IncrementalValuesProvider<PairConfigurationDiscoveryModel>
        Build(
            IncrementalValueProvider<CompilationContext> compilationContext,
            IncrementalValuesProvider<TypeMapperConfigureInfo> configureInfos)
    {
        return configureInfos
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                TryBuild(source, cancellationToken))
            .WhereHasValue()
            .WithTrackingName(
                MorphantGeneratorStageNames
                    .BuildPairConfigurationDiscoveryModels);
    }

    private static PairConfigurationDiscoveryModel? TryBuild(
        (
            TypeMapperConfigureInfo ConfigureInfo,
            CompilationContext Context
        ) source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (configureInfo, context) = source;

        if (context.KnownSymbols is not { } knownSymbols ||
            configureInfo.Syntax.ParameterList.Parameters.Count != 1)
        {
            return null;
        }

        var semanticModel = context.Compilation.GetSemanticModel(
            configureInfo.Syntax.SyntaxTree);
        var builderParameterSyntax =
            configureInfo.Syntax.ParameterList.Parameters[0];

        if (semanticModel.GetDeclaredSymbol(
                builderParameterSyntax,
                cancellationToken) is not IParameterSymbol builderParameter)
        {
            return null;
        }

        var chains = FindInvocationChains(
            configureInfo.Syntax,
            semanticModel,
            builderParameter,
            knownSymbols,
            cancellationToken);
        var registrations =
            ImmutableArray.CreateBuilder<MappingPairRegistrationModel>();

        foreach (var chain in chains)
        {
            foreach (var invocation in chain.Invocations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsMapInvocationCandidate(invocation) ||
                    semanticModel.GetSymbolInfo(
                        invocation,
                        cancellationToken).Symbol is not IMethodSymbol method ||
                    !IsMapperBuilderMapMethod(method, knownSymbols))
                {
                    continue;
                }

                registrations.Add(
                    new MappingPairRegistrationModel(
                        invocation,
                        method.TypeArguments[0],
                        method.TypeArguments[1]));
            }
        }

        return new PairConfigurationDiscoveryModel(
            configureInfo,
            new MapperMappingRegistrationModel(
                configureInfo.Syntax,
                registrations.ToImmutable()),
            chains);
    }

    private static ImmutableArray<PairConfigurationInvocationChain>
        FindInvocationChains(
            MethodDeclarationSyntax configureSyntax,
            SemanticModel semanticModel,
            IParameterSymbol builderParameter,
            KnownSymbols knownSymbols,
            CancellationToken cancellationToken)
    {
        var result =
            ImmutableArray.CreateBuilder<PairConfigurationInvocationChain>();

        if (configureSyntax.Body is { } body)
        {
            foreach (var statement in body.Statements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (statement is not ExpressionStatementSyntax
                    {
                        Expression: var expression
                    })
                {
                    continue;
                }

                if (TryBuildInvocationChain(
                        expression,
                        semanticModel,
                        builderParameter,
                        knownSymbols,
                        cancellationToken,
                        out var chain))
                {
                    result.Add(chain);
                }
            }
        }
        else if (configureSyntax.ExpressionBody is
                 {
                     Expression: var expression
                 } &&
                 TryBuildInvocationChain(
                     expression,
                     semanticModel,
                     builderParameter,
                     knownSymbols,
                     cancellationToken,
                     out var chain))
        {
            result.Add(chain);
        }

        return result.ToImmutable();
    }

    private static bool TryBuildInvocationChain(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken,
        out PairConfigurationInvocationChain chain)
    {
        if (ContainsLogicalBranchingOutsideLambdas(
                expression,
                cancellationToken))
        {
            chain = default;
            return false;
        }

        var invocations = new Stack<InvocationExpressionSyntax>();
        var current = UnwrapParentheses(expression);

        while (current is InvocationExpressionSyntax invocation)
        {
            cancellationToken.ThrowIfCancellationRequested();
            invocations.Push(invocation);

            if (invocation.Expression is not MemberAccessExpressionSyntax
                {
                    Expression: var receiver
                })
            {
                chain = default;
                return false;
            }

            receiver = UnwrapParentheses(receiver);

            if (receiver is InvocationExpressionSyntax)
            {
                current = receiver;
                continue;
            }

            if (receiver is not IdentifierNameSyntax builderIdentifier ||
                !SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(
                        builderIdentifier,
                        cancellationToken).Symbol,
                    builderParameter) ||
                !IsMapperBuilderRootInvocation(
                    invocation,
                    semanticModel,
                    knownSymbols,
                    cancellationToken) ||
                ContainsBuilderReferenceInArguments(
                    invocations,
                    semanticModel,
                    builderParameter,
                    cancellationToken))
            {
                chain = default;
                return false;
            }

            chain = new PairConfigurationInvocationChain(
                invocations.ToImmutableArray());
            return true;
        }

        chain = default;
        return false;
    }

    private static bool ContainsBuilderReferenceInArguments(
        IEnumerable<InvocationExpressionSyntax> invocations,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        CancellationToken cancellationToken)
    {
        foreach (var invocation in invocations)
        {
            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                foreach (var identifier in argument.Expression
                             .DescendantNodesAndSelf()
                             .OfType<IdentifierNameSyntax>())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (identifier.Identifier.ValueText ==
                            builderParameter.Name &&
                        SymbolEqualityComparer.Default.Equals(
                            semanticModel.GetSymbolInfo(
                                identifier,
                                cancellationToken).Symbol,
                            builderParameter))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool ContainsLogicalBranchingOutsideLambdas(
        ExpressionSyntax expression,
        CancellationToken cancellationToken)
    {
        foreach (var node in expression.DescendantNodesAndSelf(
                     static node =>
                         node is not AnonymousFunctionExpressionSyntax))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (node is ConditionalExpressionSyntax or
                SwitchExpressionSyntax or
                ConditionalAccessExpressionSyntax ||
                node.IsKind(SyntaxKind.LogicalAndExpression) ||
                node.IsKind(SyntaxKind.LogicalOrExpression) ||
                node.IsKind(SyntaxKind.CoalesceExpression) ||
                node.IsKind(SyntaxKind.CoalesceAssignmentExpression))
            {
                return true;
            }
        }

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

    private static bool IsMapperBuilderRootInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        if (semanticModel.GetSymbolInfo(
                invocation,
                cancellationToken).Symbol is not IMethodSymbol method ||
            method.IsStatic)
        {
            return false;
        }

        for (var type = knownSymbols.MapperBuilder;
             type is not null &&
             type.SpecialType != SpecialType.System_Object;
             type = type.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    method.ContainingType.OriginalDefinition,
                    type.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMapInvocationCandidate(
        InvocationExpressionSyntax invocation)
    {
        return invocation is
        {
            ArgumentList.Arguments.Count: <= 1,
            Expression: MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax
                {
                    Identifier.ValueText: "Map",
                    TypeArgumentList.Arguments.Count: 2
                }
            }
        };
    }

    private static bool IsMapperBuilderMapMethod(
        IMethodSymbol method,
        KnownSymbols knownSymbols)
    {
        return method.Name == "Map" &&
               method.MethodKind == MethodKind.Ordinary &&
               !method.IsStatic &&
               method.Parameters.Length == 1 &&
               method.TypeArguments.Length == 2 &&
               SymbolEqualityComparer.Default.Equals(
                   method.ContainingType,
                   knownSymbols.MapperBuilder);
    }
}
