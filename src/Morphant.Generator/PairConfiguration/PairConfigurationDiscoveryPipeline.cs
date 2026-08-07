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

        if (context.KnownSymbols is not { } knownSymbols)
        {
            return null;
        }

        var levels =
            ImmutableArray.CreateBuilder<PairConfigurationDiscoveryLevel>();
        var currentInfo = configureInfo;
        var currentConstructedType = configureInfo.MapperType;
        var hasUnavailableBaseConfiguration = false;
        var visitedMethods = new HashSet<IMethodSymbol>(
            SymbolEqualityComparer.Default);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryBuildLevel(
                    currentInfo,
                    currentConstructedType,
                    context,
                    knownSymbols,
                    cancellationToken,
                    out var level))
            {
                return null;
            }

            levels.Add(level);
            if (level.BaseConfigureCalls.IsEmpty)
            {
                break;
            }

            if (!TryGetConnectedBaseConfigure(
                    level,
                    context.Compilation,
                    cancellationToken,
                    out var baseInfo,
                    out var constructedBaseType) ||
                !visitedMethods.Add(
                    GetDeclaredConfigureMethod(
                        baseInfo,
                        context.Compilation,
                        cancellationToken)))
            {
                hasUnavailableBaseConfiguration = true;
                break;
            }

            currentInfo = baseInfo;
            currentConstructedType = constructedBaseType;
        }

        return new PairConfigurationDiscoveryModel(
            configureInfo,
            levels[0].InstantiatedRegistrations,
            levels.ToImmutable(),
            hasUnavailableBaseConfiguration);
    }

    private static bool TryBuildLevel(
        TypeMapperConfigureInfo configureInfo,
        INamedTypeSymbol constructedMapperType,
        CompilationContext context,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken,
        out PairConfigurationDiscoveryLevel level)
    {
        if (configureInfo.Syntax.ParameterList.Parameters.Count != 1)
        {
            level = default;
            return false;
        }

        var semanticModel = context.Compilation.GetSemanticModel(
            configureInfo.Syntax.SyntaxTree);
        var builderParameterSyntax =
            configureInfo.Syntax.ParameterList.Parameters[0];

        if (semanticModel.GetDeclaredSymbol(
                builderParameterSyntax,
                cancellationToken) is not IParameterSymbol builderParameter)
        {
            level = default;
            return false;
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

        var immutableRegistrations = registrations.ToImmutable();
        var substitutions = MapperTypeSubstitution.Build(
            configureInfo.MapperType,
            constructedMapperType);
        var instantiatedRegistrations = immutableRegistrations
            .Select(registration =>
                registration with
                {
                    SourceType = MapperTypeSubstitution.Substitute(
                        registration.SourceType,
                        substitutions,
                        context.Compilation),
                    DestinationType = MapperTypeSubstitution.Substitute(
                        registration.DestinationType,
                        substitutions,
                        context.Compilation)
                })
            .ToImmutableArray();

        level = new PairConfigurationDiscoveryLevel(
            configureInfo,
            constructedMapperType,
            new MapperMappingRegistrationModel(
                configureInfo.Syntax,
                immutableRegistrations),
            new MapperMappingRegistrationModel(
                configureInfo.Syntax,
                instantiatedRegistrations),
            chains,
            FindBaseConfigureCalls(
                configureInfo.Syntax,
                semanticModel,
                builderParameter,
                knownSymbols,
                cancellationToken));
        return true;
    }

    private static ImmutableArray<InvocationExpressionSyntax>
        FindBaseConfigureCalls(
            MethodDeclarationSyntax configureSyntax,
            SemanticModel semanticModel,
            IParameterSymbol builderParameter,
            KnownSymbols knownSymbols,
            CancellationToken cancellationToken)
    {
        if (configureSyntax.ExpressionBody?.Expression is { } expression)
        {
            return IsBaseConfigureCall(
                    expression,
                    semanticModel,
                    builderParameter,
                    knownSymbols,
                    cancellationToken,
                    out var invocation)
                ? [invocation]
                : [];
        }

        if (configureSyntax.Body is null)
        {
            return [];
        }

        var result =
            ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();

        foreach (var statement in configureSyntax.Body.Statements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (statement is not ExpressionStatementSyntax
                {
                    Expression: var statementExpression
                } ||
                !IsBaseConfigureCall(
                    statementExpression,
                    semanticModel,
                    builderParameter,
                    knownSymbols,
                    cancellationToken,
                    out var invocation))
            {
                continue;
            }

            result.Add(invocation);
        }

        return result.ToImmutable();
    }

    private static bool IsBaseConfigureCall(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken,
        out InvocationExpressionSyntax invocation)
    {
        expression = UnwrapParentheses(expression);

        if (expression is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Expression: BaseExpressionSyntax,
                    Name.Identifier.ValueText: "Configure"
                },
                ArgumentList.Arguments.Count: 1
            } candidate ||
            semanticModel.GetSymbolInfo(
                candidate,
                cancellationToken).Symbol is not IMethodSymbol method ||
            !IsTypeMapperConfigureOverride(method, knownSymbols) ||
            !ReferencesBuilderParameter(
                candidate.ArgumentList.Arguments[0].Expression,
                semanticModel,
                builderParameter,
                cancellationToken))
        {
            invocation = null!;
            return false;
        }

        invocation = candidate;
        return true;
    }

    private static bool TryGetConnectedBaseConfigure(
        PairConfigurationDiscoveryLevel level,
        Compilation compilation,
        CancellationToken cancellationToken,
        out TypeMapperConfigureInfo configureInfo,
        out INamedTypeSymbol constructedBaseType)
    {
        var semanticModel = compilation.GetSemanticModel(
            level.ConfigureInfo.Syntax.SyntaxTree);

        if (semanticModel.GetSymbolInfo(
                level.BaseConfigureCalls[0],
                cancellationToken).Symbol is not IMethodSymbol method)
        {
            configureInfo = default;
            constructedBaseType = null!;
            return false;
        }

        var resolvedConstructedBaseType = FindConstructedBaseType(
            level.ConstructedMapperType,
            method.ContainingType);

        if (resolvedConstructedBaseType is null)
        {
            configureInfo = default;
            constructedBaseType = null!;
            return false;
        }

        constructedBaseType = resolvedConstructedBaseType;

        foreach (var syntaxReference in method.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!compilation.SyntaxTrees.Contains(
                    syntaxReference.SyntaxTree) ||
                syntaxReference.GetSyntax(cancellationToken) is not
                    MethodDeclarationSyntax syntax ||
                syntax.Body is null && syntax.ExpressionBody is null)
            {
                continue;
            }

            var baseSemanticModel = compilation.GetSemanticModel(
                syntax.SyntaxTree);

            if (syntax.Parent is ClassDeclarationSyntax declaration &&
                baseSemanticModel.GetDeclaredSymbol(
                    declaration,
                    cancellationToken) is INamedTypeSymbol mapperType)
            {
                configureInfo = new TypeMapperConfigureInfo(
                    syntax,
                    mapperType);
                return true;
            }
        }

        configureInfo = default;
        return false;
    }

    private static INamedTypeSymbol? FindConstructedBaseType(
        INamedTypeSymbol mapperType,
        INamedTypeSymbol declaringType)
    {
        for (var current = mapperType.BaseType;
             current is not null;
             current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    current.OriginalDefinition,
                    declaringType.OriginalDefinition))
            {
                return current;
            }
        }

        return null;
    }

    private static IMethodSymbol GetDeclaredConfigureMethod(
        TypeMapperConfigureInfo configureInfo,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        return (IMethodSymbol)compilation.GetSemanticModel(
                configureInfo.Syntax.SyntaxTree)
            .GetDeclaredSymbol(
                configureInfo.Syntax,
                cancellationToken)!;
    }

    private static bool ReferencesBuilderParameter(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        CancellationToken cancellationToken)
    {
        expression = UnwrapParentheses(expression);

        return expression is IdentifierNameSyntax identifier &&
               SymbolEqualityComparer.Default.Equals(
                   semanticModel.GetSymbolInfo(
                       identifier,
                       cancellationToken).Symbol,
                   builderParameter);
    }

    private static bool IsTypeMapperConfigureOverride(
        IMethodSymbol method,
        KnownSymbols knownSymbols)
    {
        if (method.IsStatic ||
            !method.ReturnsVoid ||
            method.TypeParameters.Length != 0 ||
            method.Parameters.Length != 1 ||
            !SymbolEqualityComparer.Default.Equals(
                method.Parameters[0].Type,
                knownSymbols.MapperBuilder))
        {
            return false;
        }

        for (var overridden = method;
             overridden is not null;
             overridden = overridden.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    overridden.OriginalDefinition,
                    knownSymbols.TypeMapperConfigure.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
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
