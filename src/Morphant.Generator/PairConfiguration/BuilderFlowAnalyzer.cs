using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.PairConfiguration;

internal static class BuilderFlowAnalyzer
{
    private static readonly ImmutableHashSet<string> CallbackMethodNames =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "Construct",
            "Resolve",
            "ConstructUsing",
            "ResolveUsing",
            "Members",
            "IncludeMembers",
            "Convert");

    private static readonly ImmutableHashSet<string> PairMethodNames =
        CallbackMethodNames
            .Add("IncludeBase")
            .Add("NullSourceHandling")
            .Add("NullDestinationHandling")
            .Add("ConstructorSelection")
            .Add("MemberSelection")
            .Add("Flattening")
            .Add("UnmappedMemberValidation");

    public static BuilderFlowLevelAnalysis Build(
        MethodDeclarationSyntax configureSyntax,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        var ownedCallbackArguments = FindOwnedCallbackArguments(
            configureSyntax,
            semanticModel,
            knownSymbols,
            cancellationToken);
        var registrations = FindVisibleRegistrations(
            configureSyntax,
            semanticModel,
            knownSymbols,
            ownedCallbackArguments,
            cancellationToken);
        var linearFlow = FindLinearFlow(
            configureSyntax,
            semanticModel,
            builderParameter,
            knownSymbols,
            ownedCallbackArguments,
            cancellationToken);
        var breaks = FindFlowBreaks(
            configureSyntax,
            semanticModel,
            builderParameter,
            knownSymbols,
            ownedCallbackArguments,
            registrations,
            linearFlow,
            cancellationToken);

        return new BuilderFlowLevelAnalysis(
            registrations,
            linearFlow.InvocationChains,
            linearFlow.BaseConfigureCalls,
            breaks);
    }

    private static ImmutableHashSet<SyntaxNode>
        FindOwnedCallbackArguments(
        MethodDeclarationSyntax configureSyntax,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        var result = ImmutableHashSet.CreateBuilder<SyntaxNode>(
            SyntaxNodeLocationComparer.Instance);
        var root = (SyntaxNode?)configureSyntax.Body ??
                   configureSyntax.ExpressionBody?.Expression;

        if (root is null)
        {
            return result.ToImmutable();
        }

        foreach (var invocation in root.DescendantNodesAndSelf()
                     .OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (invocation.ArgumentList.Arguments.Count != 1 ||
                GetInvocationName(invocation) is not { } name ||
                !CallbackMethodNames.Contains(name.Identifier.ValueText) ||
                !IsPotentialPairCallbackInvocation(
                    invocation,
                    semanticModel,
                    knownSymbols,
                    cancellationToken))
            {
                continue;
            }

            AddOwnedCallbackRoot(
                invocation.ArgumentList.Arguments[0].Expression);
        }

        return result.ToImmutable();

        void AddOwnedCallbackRoot(SyntaxNode callbackRoot)
        {
            if (!result.Add(callbackRoot))
            {
                return;
            }

            foreach (var identifier in callbackRoot
                         .DescendantNodesAndSelf()
                         .OfType<IdentifierNameSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var symbolInfo = semanticModel.GetSymbolInfo(
                    identifier,
                    cancellationToken);

                if (symbolInfo.Symbol is { } symbol)
                {
                    AddOwnedSymbol(symbol);
                }
                else
                {
                    foreach (var candidate in symbolInfo.CandidateSymbols)
                    {
                        AddOwnedSymbol(candidate);
                    }
                }
            }

            void AddOwnedSymbol(ISymbol symbol)
            {
                switch (symbol)
                {
                    case ILocalSymbol local:
                        foreach (var syntaxReference in
                                 local.DeclaringSyntaxReferences)
                        {
                            if (syntaxReference.GetSyntax(cancellationToken)
                                    is VariableDeclaratorSyntax
                                    {
                                        Initializer.Value: { } initializer
                                    })
                            {
                                AddOwnedCallbackRoot(initializer);
                            }
                        }

                        return;

                    case IMethodSymbol
                        {
                            MethodKind: MethodKind.LocalFunction
                        } localFunction:
                        foreach (var syntaxReference in
                                 localFunction.DeclaringSyntaxReferences)
                        {
                            if (syntaxReference.GetSyntax(cancellationToken)
                                    is LocalFunctionStatementSyntax syntax)
                            {
                                AddOwnedCallbackRoot(syntax);
                            }
                        }

                        return;
                }
            }
        }
    }

    private static bool IsPotentialPairCallbackInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        var symbol = semanticModel.GetSymbolInfo(
            invocation,
            cancellationToken).Symbol as IMethodSymbol;

        if (symbol is not null)
        {
            return IsGeneratedConfigurationMethod(symbol) ||
                   IsIncludeMembersMethod(symbol);
        }

        var receiver = GetInvocationReceiver(invocation);

        if (receiver is not null &&
            IsPairBuilderType(semanticModel.GetTypeInfo(
                receiver,
                cancellationToken).Type))
        {
            return true;
        }

        return receiver is not null && receiver
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(candidate => IsMapperBuilderMapMethod(
                semanticModel.GetSymbolInfo(candidate, cancellationToken)
                    .Symbol as IMethodSymbol,
                knownSymbols));
    }

    private static ImmutableArray<MappingPairRegistrationModel>
        FindVisibleRegistrations(
        MethodDeclarationSyntax configureSyntax,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        ImmutableHashSet<SyntaxNode> ownedCallbackArguments,
        CancellationToken cancellationToken)
    {
        var root = (SyntaxNode?)configureSyntax.Body ??
                   configureSyntax.ExpressionBody?.Expression;

        if (root is null)
        {
            return ImmutableArray<MappingPairRegistrationModel>.Empty;
        }

        var result = ImmutableArray.CreateBuilder<
            MappingPairRegistrationModel>();

        foreach (var invocation in root.DescendantNodesAndSelf(node =>
                     ShouldDescendForRegistration(
                         node,
                         ownedCallbackArguments))
                     .OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (semanticModel.GetSymbolInfo(
                    invocation,
                    cancellationToken).Symbol is not IMethodSymbol method ||
                !IsMapperBuilderMapMethod(method, knownSymbols))
            {
                continue;
            }

            result.Add(new MappingPairRegistrationModel(
                invocation,
                method.TypeArguments[0],
                method.TypeArguments[1]));
        }

        return result
            .OrderBy(static registration => registration.Syntax.SpanStart)
            .ToImmutableArray();
    }

    private static bool ShouldDescendForRegistration(
        SyntaxNode node,
        ImmutableHashSet<SyntaxNode> ownedCallbackArguments)
    {
        return node is not LocalFunctionStatementSyntax &&
               node is not AnonymousFunctionExpressionSyntax &&
               (node is not ExpressionSyntax expression ||
                !ownedCallbackArguments.Contains(expression));
    }

    private static LinearFlow FindLinearFlow(
        MethodDeclarationSyntax configureSyntax,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        KnownSymbols knownSymbols,
        ImmutableHashSet<SyntaxNode> ownedCallbackArguments,
        CancellationToken cancellationToken)
    {
        var invocationChains =
            ImmutableArray.CreateBuilder<PairConfigurationInvocationChain>();
        var baseConfigureCalls =
            ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();

        if (configureSyntax.ExpressionBody?.Expression is { } expression)
        {
            AddLinearExpression(expression);
        }
        else if (configureSyntax.Body is { } body)
        {
            var followingMayBeSkipped = false;

            foreach (var statement in body.Statements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!followingMayBeSkipped &&
                    statement is ExpressionStatementSyntax
                    {
                        Expression: var statementExpression
                    })
                {
                    AddLinearExpression(statementExpression);
                }

                followingMayBeSkipped |= MayPreventFollowingExecution(
                    statement,
                    cancellationToken);
            }
        }

        return new LinearFlow(
            invocationChains.ToImmutable(),
            baseConfigureCalls.ToImmutable());

        void AddLinearExpression(ExpressionSyntax linearExpression)
        {
            if (TryGetBaseConfigureCall(
                    linearExpression,
                    semanticModel,
                    builderParameter,
                    knownSymbols,
                    cancellationToken,
                    out var baseConfigureCall))
            {
                baseConfigureCalls.Add(baseConfigureCall);
                return;
            }

            if (TryBuildInvocationChain(
                    linearExpression,
                    semanticModel,
                    builderParameter,
                    knownSymbols,
                    ownedCallbackArguments,
                    cancellationToken,
                    out var chain))
            {
                invocationChains.Add(chain);
            }
        }
    }

    private static bool MayPreventFollowingExecution(
        StatementSyntax statement,
        CancellationToken cancellationToken)
    {
        if (statement is WhileStatementSyntax or
            DoStatementSyntax or
            ForStatementSyntax or
            ForEachStatementSyntax)
        {
            return true;
        }

        foreach (var node in statement.DescendantNodesAndSelf(
                     static node =>
                         node is not LocalFunctionStatementSyntax &&
                         node is not AnonymousFunctionExpressionSyntax))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (node is ReturnStatementSyntax or
                ThrowStatementSyntax or
                GotoStatementSyntax or
                YieldStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static ImmutableArray<BuilderFlowBreakModel> FindFlowBreaks(
        MethodDeclarationSyntax configureSyntax,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        KnownSymbols knownSymbols,
        ImmutableHashSet<SyntaxNode> ownedCallbackArguments,
        ImmutableArray<MappingPairRegistrationModel> registrations,
        LinearFlow linearFlow,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<BuilderFlowBreakModel>();
        var validNodes = linearFlow.InvocationChains
            .SelectMany(static chain => chain.Invocations)
            .Cast<SyntaxNode>()
            .Concat(linearFlow.BaseConfigureCalls)
            .ToImmutableArray();
        var validRegistrationLocations = new HashSet<InvocationKey>(
            linearFlow.InvocationChains
                .SelectMany(static chain => chain.Invocations)
                .Select(static invocation =>
                    InvocationKey.Create(invocation)));
        var pairBreakRootReferences = new HashSet<InvocationKey>();

        foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (validRegistrationLocations.Contains(
                    InvocationKey.Create(registration.Syntax)) ||
                !TryGetDirectRootReference(
                    registration.Syntax,
                    semanticModel,
                    builderParameter,
                    knownSymbols,
                    cancellationToken,
                    out var rootReference) ||
                !IsDirectlyExecuted(
                    registration.Syntax,
                    configureSyntax,
                    cancellationToken))
            {
                continue;
            }

            var location = FindFirstThirdPartyPairMethodLocation(
                    registration.Syntax,
                    semanticModel,
                    knownSymbols,
                    cancellationToken) ??
                GetInvocationName(registration.Syntax)?.Identifier
                    .GetLocation() ??
                registration.Syntax.GetLocation();

            result.Add(new BuilderFlowBreakModel(
                BuilderFlowBreakKind.Mapping,
                location,
                registration,
                LevelOrder: 0));
            pairBreakRootReferences.Add(InvocationKey.Create(rootReference));
        }

        var root = (SyntaxNode?)configureSyntax.Body ??
                   configureSyntax.ExpressionBody?.Expression;

        if (root is null)
        {
            return result.ToImmutable();
        }

        var rootBreakUnits = new HashSet<InvocationKey>();

        foreach (var identifier in root.DescendantNodesAndSelf(node =>
                     !ownedCallbackArguments.Contains(node))
                     .OfType<IdentifierNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(
                        identifier,
                        cancellationToken).Symbol,
                    builderParameter) ||
                IsInsideNameOf(identifier) ||
                IsCompilerOwnedBuilderUse(
                    identifier,
                    semanticModel,
                    cancellationToken) ||
                validNodes.Any(node => node.SyntaxTree == identifier.SyntaxTree &&
                    node.Span.Contains(identifier.Span)) ||
                pairBreakRootReferences.Contains(
                    InvocationKey.Create(identifier)))
            {
                continue;
            }

            var unit = GetFlowBreakUnit(identifier, configureSyntax);
            var unitKey = InvocationKey.Create(unit);

            if (!rootBreakUnits.Add(unitKey))
            {
                continue;
            }

            result.Add(new BuilderFlowBreakModel(
                BuilderFlowBreakKind.Mapper,
                FindRootBreakLocation(
                    identifier,
                    unit,
                    semanticModel,
                    knownSymbols,
                    cancellationToken),
                Registration: null,
                LevelOrder: 0));
        }

        return result
            .OrderBy(static flowBreak => flowBreak.Location.SourceSpan.Start)
            .ThenBy(static flowBreak => flowBreak.Kind)
            .ToImmutableArray();
    }

    private static bool IsDirectlyExecuted(
        InvocationExpressionSyntax invocation,
        MethodDeclarationSyntax configureSyntax,
        CancellationToken cancellationToken)
    {
        if (configureSyntax.ExpressionBody?.Expression is { } expression)
        {
            return !HasNonLinearAncestor(invocation, expression);
        }

        if (configureSyntax.Body is not { } body ||
            GetTopLevelStatement(invocation, body) is not { } statement)
        {
            return false;
        }

        if (statement is IfStatementSyntax or
            SwitchStatementSyntax or
            TryStatementSyntax or
            WhileStatementSyntax or
            DoStatementSyntax or
            ForStatementSyntax or
            ForEachStatementSyntax)
        {
            return false;
        }

        foreach (var preceding in body.Statements)
        {
            if (ReferenceEquals(preceding, statement))
            {
                break;
            }

            if (MayPreventFollowingExecution(preceding, cancellationToken))
            {
                return false;
            }
        }

        return !HasNonLinearAncestor(invocation, statement);
    }

    private static StatementSyntax? GetTopLevelStatement(
        SyntaxNode node,
        BlockSyntax body)
    {
        return node.AncestorsAndSelf()
            .OfType<StatementSyntax>()
            .FirstOrDefault(statement => ReferenceEquals(statement.Parent, body));
    }

    private static bool HasNonLinearAncestor(
        SyntaxNode node,
        SyntaxNode boundary)
    {
        for (var current = node.Parent;
             current is not null && !ReferenceEquals(current, boundary);
             current = current.Parent)
        {
            if (current is ConditionalAccessExpressionSyntax
                    conditionalAccess)
            {
                if (!conditionalAccess.Expression.Span.Contains(node.Span))
                {
                    return true;
                }

                continue;
            }

            if (current is IfStatementSyntax or
                SwitchStatementSyntax or
                TryStatementSyntax or
                CatchClauseSyntax or
                FinallyClauseSyntax or
                WhileStatementSyntax or
                DoStatementSyntax or
                ForStatementSyntax or
                ForEachStatementSyntax or
                ConditionalExpressionSyntax or
                SwitchExpressionSyntax or
                AnonymousFunctionExpressionSyntax or
                LocalFunctionStatementSyntax ||
                current.IsKind(SyntaxKind.LogicalAndExpression) ||
                current.IsKind(SyntaxKind.LogicalOrExpression) ||
                current.IsKind(SyntaxKind.CoalesceExpression) ||
                current.IsKind(SyntaxKind.CoalesceAssignmentExpression))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCompilerOwnedBuilderUse(
        IdentifierNameSyntax identifier,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var invocation in identifier.Ancestors()
                     .OfType<InvocationExpressionSyntax>())
        {
            var symbolInfo = semanticModel.GetSymbolInfo(
                invocation,
                cancellationToken);

            if (symbolInfo.Symbol is not null)
            {
                continue;
            }

            if (semanticModel.GetDiagnostics(
                    invocation.Span,
                    cancellationToken).Any(static diagnostic =>
                    diagnostic.Severity == DiagnosticSeverity.Error))
            {
                return true;
            }
        }

        return false;
    }

    private static SyntaxNode GetFlowBreakUnit(
        IdentifierNameSyntax identifier,
        MethodDeclarationSyntax configureSyntax)
    {
        var deferred = identifier.Ancestors()
            .FirstOrDefault(static node =>
                node is AnonymousFunctionExpressionSyntax or
                    LocalFunctionStatementSyntax);

        if (deferred is not null)
        {
            return deferred;
        }

        if (configureSyntax.Body is { } body &&
            GetTopLevelStatement(identifier, body) is { } statement)
        {
            return statement;
        }

        return (SyntaxNode?)configureSyntax.ExpressionBody?.Expression ??
               configureSyntax;
    }

    private static Location FindRootBreakLocation(
        IdentifierNameSyntax identifier,
        SyntaxNode unit,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        if (IsValueEscape(identifier, unit, semanticModel, knownSymbols,
                cancellationToken))
        {
            return identifier.Identifier.GetLocation();
        }

        foreach (var conditionalAccess in identifier.Ancestors()
                     .OfType<ConditionalAccessExpressionSyntax>()
                     .Where(conditionalAccess =>
                         unit.Span.Contains(conditionalAccess.Span)))
        {
            var invocation = conditionalAccess.WhenNotNull
                .DescendantNodesAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault();
            var method = invocation is null
                ? null
                : semanticModel.GetSymbolInfo(
                    invocation,
                    cancellationToken).Symbol as IMethodSymbol;

            if (invocation is not null &&
                IsMapperBuilderRootMethod(method, knownSymbols))
            {
                return GetInvocationName(invocation)?.Identifier
                    .GetLocation() ?? invocation.GetLocation();
            }
        }

        var invocations = unit.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.Span.Contains(identifier.Span))
            .OrderBy(static invocation => invocation.Span.Length)
            .ToImmutableArray();

        foreach (var invocation in invocations)
        {
            var method = semanticModel.GetSymbolInfo(
                invocation,
                cancellationToken).Symbol as IMethodSymbol;

            if (method is not null &&
                !IsMapperBuilderRootMethod(method, knownSymbols) &&
                !IsTypeMapperConfigureOverride(method, knownSymbols))
            {
                return GetInvocationName(invocation)?.Identifier
                    .GetLocation() ?? invocation.GetLocation();
            }
        }

        foreach (var invocation in invocations)
        {
            var method = semanticModel.GetSymbolInfo(
                invocation,
                cancellationToken).Symbol as IMethodSymbol;

            if (IsMapperBuilderRootMethod(method, knownSymbols) ||
                IsTypeMapperConfigureOverride(method, knownSymbols))
            {
                return GetInvocationName(invocation)?.Identifier
                    .GetLocation() ?? invocation.GetLocation();
            }
        }

        return identifier.Identifier.GetLocation();
    }

    private static bool IsValueEscape(
        IdentifierNameSyntax identifier,
        SyntaxNode unit,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        if (identifier.Ancestors().TakeWhile(node => !ReferenceEquals(node, unit))
            .Any(static node => node is AnonymousFunctionExpressionSyntax or
                LocalFunctionStatementSyntax))
        {
            return true;
        }

        SyntaxNode current = identifier;

        while (current.Parent is { } parent &&
               !ReferenceEquals(parent, unit.Parent))
        {
            if (parent is ParenthesizedExpressionSyntax ||
                parent is PostfixUnaryExpressionSyntax postfix &&
                postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            {
                current = parent;
                continue;
            }

            if (parent is MemberAccessExpressionSyntax memberAccess &&
                ReferenceEquals(memberAccess.Expression, current) ||
                parent is ConditionalAccessExpressionSyntax conditional &&
                ReferenceEquals(conditional.Expression, current) ||
                parent is InvocationExpressionSyntax invocation &&
                ReferenceEquals(invocation.Expression, current))
            {
                current = parent;
                continue;
            }

            if (parent is ArgumentSyntax argument)
            {
                var target = argument.Parent?.Parent as
                    InvocationExpressionSyntax;
                var method = target is null
                    ? null
                    : semanticModel.GetSymbolInfo(target, cancellationToken)
                        .Symbol as IMethodSymbol;

                return !IsTypeMapperConfigureOverride(method, knownSymbols);
            }

            if (parent is ExpressionStatementSyntax or ArrowExpressionClauseSyntax)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static Location? FindFirstThirdPartyPairMethodLocation(
        InvocationExpressionSyntax mapInvocation,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        SyntaxNode current = mapInvocation;

        while (TryGetOuterReceiverInvocation(current, out var outer))
        {
            var method = semanticModel.GetSymbolInfo(
                outer,
                cancellationToken).Symbol as IMethodSymbol;

            if (!IsAllowedPairInvocation(
                    outer,
                    method,
                    semanticModel,
                    knownSymbols,
                    cancellationToken))
            {
                return method is null
                    ? null
                    : GetInvocationName(outer)?.Identifier.GetLocation() ??
                      outer.GetLocation();
            }

            current = outer;
        }

        return null;
    }

    private static bool TryGetOuterReceiverInvocation(
        SyntaxNode current,
        out InvocationExpressionSyntax invocation)
    {
        SyntaxNode node = current;

        while (node.Parent is ParenthesizedExpressionSyntax ||
               node.Parent is PostfixUnaryExpressionSyntax postfix &&
               postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
        {
            node = node.Parent;
        }

        if (node.Parent is MemberAccessExpressionSyntax memberAccess &&
            ReferenceEquals(memberAccess.Expression, node) &&
            memberAccess.Parent is InvocationExpressionSyntax outer)
        {
            invocation = outer;
            return true;
        }

        invocation = null!;
        return false;
    }

    private static bool TryGetDirectRootReference(
        InvocationExpressionSyntax mapInvocation,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken,
        out IdentifierNameSyntax identifier)
    {
        if (GetInvocationReceiver(mapInvocation) is not { } receiver)
        {
            identifier = null!;
            return false;
        }

        receiver = UnwrapTransparent(receiver);

        while (receiver is InvocationExpressionSyntax invocation)
        {
            var method = semanticModel.GetSymbolInfo(
                invocation,
                cancellationToken).Symbol as IMethodSymbol;

            if (!IsMapperBuilderRootMethod(method, knownSymbols) ||
                GetInvocationReceiver(invocation) is not { } next)
            {
                identifier = null!;
                return false;
            }

            receiver = UnwrapTransparent(next);
        }

        if (receiver is IdentifierNameSyntax candidate &&
            SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(candidate, cancellationToken).Symbol,
                builderParameter))
        {
            identifier = candidate;
            return true;
        }

        identifier = null!;
        return false;
    }

    private static bool TryBuildInvocationChain(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        KnownSymbols knownSymbols,
        ImmutableHashSet<SyntaxNode> ownedCallbackArguments,
        CancellationToken cancellationToken,
        out PairConfigurationInvocationChain chain)
    {
        if (ContainsLogicalBranchingOutsideCallbacks(
                expression,
                ownedCallbackArguments,
                cancellationToken))
        {
            chain = default;
            return false;
        }

        var invocations = new Stack<InvocationExpressionSyntax>();
        var current = UnwrapTransparent(expression);

        while (current is InvocationExpressionSyntax invocation)
        {
            cancellationToken.ThrowIfCancellationRequested();
            invocations.Push(invocation);

            if (GetInvocationReceiver(invocation) is not { } receiver)
            {
                chain = default;
                return false;
            }

            receiver = UnwrapTransparent(receiver);

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
                ContainsBuilderReferenceInArguments(
                    invocations,
                    semanticModel,
                    builderParameter,
                    ownedCallbackArguments,
                    cancellationToken))
            {
                chain = default;
                return false;
            }

            var immutableInvocations = invocations.ToImmutableArray();

            if (!IsSupportedInvocationSequence(
                    immutableInvocations,
                    semanticModel,
                    knownSymbols,
                    cancellationToken))
            {
                chain = default;
                return false;
            }

            chain = new PairConfigurationInvocationChain(
                immutableInvocations);
            return true;
        }

        chain = default;
        return false;
    }

    private static bool IsSupportedInvocationSequence(
        ImmutableArray<InvocationExpressionSyntax> invocations,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        var reachedMap = false;

        foreach (var invocation in invocations)
        {
            var method = semanticModel.GetSymbolInfo(
                invocation,
                cancellationToken).Symbol as IMethodSymbol;

            if (IsMapperBuilderMapMethod(method, knownSymbols))
            {
                if (reachedMap)
                {
                    return false;
                }

                reachedMap = true;
                continue;
            }

            if (!reachedMap)
            {
                if (!IsMapperBuilderRootMethod(method, knownSymbols))
                {
                    return false;
                }

                continue;
            }

            if (!IsAllowedPairInvocation(
                    invocation,
                    method,
                    semanticModel,
                    knownSymbols,
                    cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllowedPairInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol? method,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        if (method is not null)
        {
            if (IsGeneratedConfigurationMethod(method))
            {
                return true;
            }

            var containingType = method.ContainingType.OriginalDefinition;

            return SymbolEqualityComparer.Default.Equals(
                       containingType,
                       knownSymbols.MapperBuilderBase) ||
                   IsPairBuilderType(containingType);
        }

        if (GetInvocationName(invocation) is not { } name ||
            !PairMethodNames.Contains(name.Identifier.ValueText))
        {
            return true;
        }

        var receiver = GetInvocationReceiver(invocation);
        var receiverType = receiver is null
            ? null
            : semanticModel.GetTypeInfo(receiver, cancellationToken).Type;

        return receiverType is null ||
               receiverType is IErrorTypeSymbol ||
               IsPairBuilderType(receiverType);
    }

    private static bool ContainsBuilderReferenceInArguments(
        IEnumerable<InvocationExpressionSyntax> invocations,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        ImmutableHashSet<SyntaxNode> ownedCallbackArguments,
        CancellationToken cancellationToken)
    {
        foreach (var invocation in invocations)
        {
            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                if (ownedCallbackArguments.Contains(argument.Expression))
                {
                    continue;
                }

                foreach (var identifier in argument.Expression
                             .DescendantNodesAndSelf()
                             .OfType<IdentifierNameSyntax>())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (SymbolEqualityComparer.Default.Equals(
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

    private static bool ContainsLogicalBranchingOutsideCallbacks(
        ExpressionSyntax expression,
        ImmutableHashSet<SyntaxNode> ownedCallbackArguments,
        CancellationToken cancellationToken)
    {
        foreach (var node in expression.DescendantNodesAndSelf(candidate =>
                     candidate is not ExpressionSyntax candidateExpression ||
                     !ownedCallbackArguments.Contains(candidateExpression)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (node is ExpressionSyntax nodeExpression &&
                ownedCallbackArguments.Contains(nodeExpression))
            {
                continue;
            }

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

    private static bool TryGetBaseConfigureCall(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken,
        out InvocationExpressionSyntax invocation)
    {
        expression = UnwrapTransparent(expression);

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

    private static bool ReferencesBuilderParameter(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        CancellationToken cancellationToken)
    {
        expression = UnwrapTransparent(expression);

        return expression is IdentifierNameSyntax identifier &&
               SymbolEqualityComparer.Default.Equals(
                   semanticModel.GetSymbolInfo(
                       identifier,
                       cancellationToken).Symbol,
                   builderParameter);
    }

    private static bool IsMapperBuilderRootMethod(
        IMethodSymbol? method,
        KnownSymbols knownSymbols)
    {
        if (method is null || method.IsStatic)
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

    private static bool IsMapperBuilderMapMethod(
        IMethodSymbol? method,
        KnownSymbols knownSymbols)
    {
        return method is
        {
            Name: "Map",
            MethodKind: MethodKind.Ordinary,
            IsStatic: false,
            Parameters.Length: 1,
            TypeArguments.Length: 2
        } &&
        SymbolEqualityComparer.Default.Equals(
            method.ContainingType,
            knownSymbols.MapperBuilder);
    }

    private static bool IsTypeMapperConfigureOverride(
        IMethodSymbol? method,
        KnownSymbols knownSymbols)
    {
        if (method is null ||
            method.IsStatic ||
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

    private static bool IsGeneratedConfigurationMethod(IMethodSymbol method)
    {
        var definition = method.ReducedFrom ?? method;

        return CallbackMethodNames.Contains(method.Name) &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       definition.ContainingType),
                   MetadataNames.GeneratedMappingExtensions);
    }

    private static bool IsIncludeMembersMethod(IMethodSymbol method)
    {
        return method.Name == "IncludeMembers" &&
               method.MethodKind == MethodKind.Ordinary &&
               !method.IsStatic &&
               method.Parameters.Length == 1 &&
               method.TypeArguments.Length == 0 &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       method.ContainingType.OriginalDefinition),
                   MetadataNames.PairMapperBuilder);
    }

    private static bool IsPairBuilderType(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol namedType &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       namedType.OriginalDefinition),
                   MetadataNames.PairMapperBuilder);
    }

    private static ExpressionSyntax? GetInvocationReceiver(
        InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Expression,
            _ => null
        };
    }

    private static SimpleNameSyntax? GetInvocationName(
        InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            SimpleNameSyntax name => name,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
            _ => null
        };
    }

    private static ExpressionSyntax UnwrapTransparent(
        ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;

                case PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(
                        SyntaxKind.SuppressNullableWarningExpression):
                    expression = postfix.Operand;
                    continue;

                default:
                    return expression;
            }
        }
    }

    private static bool IsInsideNameOf(IdentifierNameSyntax identifier)
    {
        return identifier.Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .Any(static invocation => invocation.Expression is
                IdentifierNameSyntax { Identifier.ValueText: "nameof" });
    }

    private readonly record struct LinearFlow(
        ImmutableArray<PairConfigurationInvocationChain> InvocationChains,
        ImmutableArray<InvocationExpressionSyntax> BaseConfigureCalls);

    private readonly record struct InvocationKey(
        SyntaxTree SyntaxTree,
        int Start,
        int Length)
    {
        public static InvocationKey Create(SyntaxNode node) =>
            new(node.SyntaxTree, node.SpanStart, node.Span.Length);
    }

    private sealed class SyntaxNodeLocationComparer :
        IEqualityComparer<SyntaxNode>
    {
        public static SyntaxNodeLocationComparer Instance { get; } = new();

        public bool Equals(SyntaxNode? left, SyntaxNode? right)
        {
            return ReferenceEquals(left, right) ||
                   left is not null &&
                   right is not null &&
                   left.SyntaxTree == right.SyntaxTree &&
                   left.Span == right.Span;
        }

        public int GetHashCode(SyntaxNode node)
        {
            unchecked
            {
                return (node.SyntaxTree.GetHashCode() * 397) ^
                       node.Span.GetHashCode();
            }
        }
    }
}

internal readonly record struct BuilderFlowLevelAnalysis(
    ImmutableArray<MappingPairRegistrationModel> Registrations,
    ImmutableArray<PairConfigurationInvocationChain> InvocationChains,
    ImmutableArray<InvocationExpressionSyntax> BaseConfigureCalls,
    ImmutableArray<BuilderFlowBreakModel> FlowBreaks);
