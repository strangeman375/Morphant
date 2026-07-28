using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.MapperBuilderMap;

internal static class MapperBuilderMapPipeline
{
    public static IncrementalValuesProvider<MapperBuilderMapInfo> Build(
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValuesProvider<TypeMapperConfigureInfo> configureInfos)
    {
        return configureInfos
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                TryBuild(source, cancellationToken))
            .WhereHasValue()
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildMapperBuilderMapInfos);
    }

    private static MapperBuilderMapInfo? TryBuild(
        (
            TypeMapperConfigureInfo ConfigureInfo,
            CompilationContext Context
        ) source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (configureInfo, context) = source;

        if (context.KnownSymbols is not { } knownSymbols)
        {
            return null;
        }

        var semanticModel = context.Compilation.GetSemanticModel(
            configureInfo.Syntax.SyntaxTree);

        if (configureInfo.Syntax.ParameterList.Parameters.Count != 1)
        {
            return null;
        }

        var builderParameterSyntax =
            configureInfo.Syntax.ParameterList.Parameters[0];

        if (semanticModel.GetDeclaredSymbol(
                builderParameterSyntax,
                cancellationToken) is not IParameterSymbol builderParameter ||
            !TryGetLinearInvocations(
                configureInfo.Syntax,
                semanticModel,
                builderParameter,
                knownSymbols,
                cancellationToken,
                out var invocations))
        {
            return null;
        }

        var registrations =
            ImmutableArray.CreateBuilder<MapperBuilderMapRegistrationInfo>();
        var seen = new HashSet<MapperBuilderMapIdentity>();

        for (var invocationIndex = 0;
             invocationIndex < invocations.Length;
             invocationIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocation = invocations[invocationIndex];

            if (!IsMapInvocationCandidate(invocation) ||
                semanticModel.GetSymbolInfo(
                    invocation,
                    cancellationToken).Symbol is not IMethodSymbol method ||
                !IsMapperBuilderMapMethod(method, knownSymbols))
            {
                continue;
            }

            var sourceType = method.TypeArguments[0];
            var destinationType = method.TypeArguments[1];

            var identity = new MapperBuilderMapIdentity(
                sourceType.ToDisplayString(
                    SymbolDisplayFormats.FullyQualifiedNullable),
                destinationType.ToDisplayString(
                    SymbolDisplayFormats.FullyQualifiedNullable));

            if (seen.Add(identity))
            {
                registrations.Add(
                    new MapperBuilderMapRegistrationInfo(
                        invocation,
                        FindTemplateInvocation(
                            invocations,
                            invocationIndex + 1,
                            invocation),
                        sourceType,
                        destinationType));
            }
        }

        return new MapperBuilderMapInfo(
            configureInfo.Syntax,
            registrations.ToImmutable());
    }

    private static InvocationExpressionSyntax? FindTemplateInvocation(
        ImmutableArray<InvocationExpressionSyntax> invocations,
        int startIndex,
        InvocationExpressionSyntax mapInvocation)
    {
        for (var index = startIndex;
             index < invocations.Length;
             index++)
        {
            var invocation = invocations[index];

            if (IsMapInvocationCandidate(invocation))
            {
                return null;
            }

            if (invocation.Expression is MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "Template"
                } &&
                invocation.DescendantNodes().Contains(mapInvocation))
            {
                return invocation;
            }
        }

        return null;
    }

    private static bool TryGetLinearInvocations(
        MethodDeclarationSyntax configureSyntax,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken,
        out ImmutableArray<InvocationExpressionSyntax> invocations)
    {
        var result =
            ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();

        if (configureSyntax.Body is { } body)
        {
            foreach (var statement in body.Statements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (statement is
                    LocalDeclarationStatementSyntax or
                    LocalFunctionStatementSyntax)
                {
                    continue;
                }

                if (statement is not ExpressionStatementSyntax
                    {
                        Expression: var expression
                    } ||
                    !TryAddInvocationChain(
                        expression,
                        semanticModel,
                        builderParameter,
                        knownSymbols,
                        cancellationToken,
                        result))
                {
                    invocations = default;
                    return false;
                }
            }
        }
        else if (configureSyntax.ExpressionBody is
                 {
                     Expression: var expression
                 })
        {
            if (!TryAddInvocationChain(
                    expression,
                    semanticModel,
                    builderParameter,
                    knownSymbols,
                    cancellationToken,
                    result))
            {
                invocations = default;
                return false;
            }
        }
        else
        {
            invocations = default;
            return false;
        }

        invocations = result.ToImmutable();
        return true;
    }

    private static bool TryAddInvocationChain(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken,
        ImmutableArray<InvocationExpressionSyntax>.Builder result)
    {
        if (ContainsLogicalBranchingOutsideLambdas(
                expression,
                cancellationToken))
        {
            return false;
        }

        var chain = new Stack<InvocationExpressionSyntax>();
        var current = UnwrapParentheses(expression);

        while (current is InvocationExpressionSyntax invocation)
        {
            cancellationToken.ThrowIfCancellationRequested();
            chain.Push(invocation);

            if (invocation.Expression is not MemberAccessExpressionSyntax
                {
                    Expression: var receiver
                })
            {
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
                    cancellationToken))
            {
                return false;
            }

            if (ContainsBuilderReferenceInArguments(
                    chain,
                    semanticModel,
                    builderParameter,
                    cancellationToken))
            {
                return false;
            }

            while (chain.Count > 0)
            {
                result.Add(chain.Pop());
            }

            return true;
        }

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
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                cancellationToken.ThrowIfCancellationRequested();

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
                            builderParameter) &&
                        !IsInsideByFactoryArgument(
                            identifier,
                            semanticModel,
                            cancellationToken))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsInsideByFactoryArgument(
        IdentifierNameSyntax identifier,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var argument in identifier
                     .Ancestors()
                     .OfType<ArgumentSyntax>())
        {
            if (argument.Parent is not ArgumentListSyntax
                {
                    Parent:
                        InvocationExpressionSyntax invocation
                } ||
                semanticModel.GetSymbolInfo(
                        invocation,
                        cancellationToken)
                    .Symbol is not IMethodSymbol
                    {
                        Name: "ByFactory",
                        ContainingType: { } containingType
                    } ||
                !StringComparer.Ordinal.Equals(
                    SymbolNameHelper.GetFullMetadataName(
                        containingType),
                    "Morphant.TypeMapper"))
            {
                continue;
            }

            return true;
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

    private readonly record struct MapperBuilderMapIdentity(
        string SourceType,
        string DestinationType);
}
