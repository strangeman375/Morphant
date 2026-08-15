using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ConstructionDiagnosticAnalyzer
{
    public static ImmutableArray<ConstructionDiagnosticCandidate> Build(
        MapperContractAnalysis analysis,
        TypeMapperModel model,
        ImmutableArray<CallbackDiagnosticCandidate> callbackDiagnostics,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<
            ConstructionDiagnosticCandidate>();

        foreach (var configuration in analysis.Configuration.Pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (analysis.Excludes(configuration.Pair.Identity) ||
                configuration.Conflicts != PairConfigurationConflict.None ||
                HasEffectiveResultPolicyBindingError(
                    configuration,
                    cancellationToken) ||
                !TryGetMapping(
                    model,
                    configuration.Pair.Identity,
                    out var mapping) ||
                !CanAnalyze(mapping))
            {
                continue;
            }

            var reachablePaths = GetReachablePaths(mapping);

            if (reachablePaths == MappingExecutionPathSet.None)
            {
                continue;
            }

            var context = new ConstructionAnalysisContext(
                analysis.Configuration,
                configuration,
                mapping);

            AnalyzeMapping(
                context,
                mapping,
                reachablePaths,
                result,
                cancellationToken);
        }

        return result
            .Where(candidate => !callbackDiagnostics.Any(callback =>
                Suppresses(candidate, callback)))
            .ToImmutableArray();
    }

    private static bool Suppresses(
        ConstructionDiagnosticCandidate construction,
        CallbackDiagnosticCandidate callback)
    {
        if (!StringComparer.Ordinal.Equals(
                construction.PairKey,
                callback.PairKey))
        {
            return false;
        }

        var callbackLocation = callback.Diagnostic.Location;
        var scope = construction.ScopeLocation;

        return StringComparer.Ordinal.Equals(
                   LocationPath(scope),
                   LocationPath(callbackLocation)) &&
               scope.SourceSpan.Contains(callbackLocation.SourceSpan);
    }

    private static bool HasEffectiveResultPolicyBindingError(
        PairConfigurationModel configuration,
        CancellationToken cancellationToken)
    {
        foreach (var policy in configuration.Declarative.ResultPolicies)
        {
            if (HasSourceBindingError(
                    policy.Invocation,
                    policy.Expression.SemanticModel,
                    cancellationToken))
            {
                return true;
            }
        }

        if (configuration.LocalPlanSlots
            .Where(static occurrence =>
                occurrence.Kind == MappingPlanSlotKind.ResultPolicy)
            .Any(occurrence => !configuration.Declarative.ResultPolicies.Any(
                policy => SameSyntax(
                    policy.Invocation,
                    occurrence.Invocation))))
        {
            return true;
        }

        var representedInvocations = configuration.Declarative.ResultPolicies
            .Select(static policy => policy.Invocation)
            .Concat(configuration.Manual.Conversions.Select(
                static conversion => conversion.Invocation))
            .ToImmutableArray();

        return EnumerateFluentResultPolicies(
                configuration.Pair.Registration.Syntax)
            .Any(invocation => !representedInvocations.Any(represented =>
                SameSyntax(represented, invocation)));
    }

    private static IEnumerable<InvocationExpressionSyntax>
        EnumerateFluentResultPolicies(
            InvocationExpressionSyntax registration)
    {
        SyntaxNode current = registration;

        while (current.Parent is MemberAccessExpressionSyntax
               {
                   Expression: var receiver
               } access &&
               ReferenceEquals(receiver, current) &&
               access.Parent is InvocationExpressionSyntax invocation &&
               ReferenceEquals(invocation.Expression, access))
        {
            var name = access.Name.Identifier.ValueText;

            if (name is
                "Construct" or
                "Resolve" or
                "ConstructUsing" or
                "ResolveUsing" or
                "Convert")
            {
                yield return invocation;
            }

            current = invocation;
        }
    }

    private static void AnalyzeMapping(
        ConstructionAnalysisContext context,
        TypeMapperMappingModel mapping,
        MappingExecutionPathSet paths,
        ImmutableArray<ConstructionDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (mapping.Failure is { } mappingFailure &&
            IsPriorCategoryFailure(mappingFailure.Reason))
        {
            return;
        }

        if (mapping.ControlFlow is { } controlFlow)
        {
            var noPrevious = paths & MappingExecutionPathSet.NoPrevious;

            if (noPrevious != MappingExecutionPathSet.None)
            {
                AnalyzeNode(
                    context,
                    controlFlow.CreateRoot,
                    noPrevious,
                    result,
                    cancellationToken);
            }

            var existing = paths &
                MappingExecutionPathSet.UpdateWithPrevious;

            if (existing != MappingExecutionPathSet.None)
            {
                AnalyzeNode(
                    context,
                    controlFlow.UpdateRoot,
                    existing,
                    result,
                    cancellationToken);
            }

            return;
        }

        AnalyzeFailure(
            context,
            mapping,
            mapping.CreateFailure,
            paths & MappingExecutionPathSet.NoPrevious,
            result,
            cancellationToken);
        AnalyzeFailure(
            context,
            mapping,
            mapping.UpdateFailure,
            paths & MappingExecutionPathSet.UpdateWithPrevious,
            result,
            cancellationToken);
    }

    private static void AnalyzeNode(
        ConstructionAnalysisContext context,
        TypeMapperControlFlowNode node,
        MappingExecutionPathSet paths,
        ImmutableArray<ConstructionDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (node.Leaf is { } leaf)
        {
            AnalyzeMapping(
                context,
                leaf,
                paths,
                result,
                cancellationToken);
            return;
        }

        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            AnalyzeNode(
                context,
                evaluationContinuation,
                paths,
                result,
                cancellationToken);
            return;
        }

        if (node.SwitchExpression is not null)
        {
            foreach (var section in node.SwitchSections)
            {
                AnalyzeNode(
                    context,
                    section.Branch,
                    paths,
                    result,
                    cancellationToken);
            }

            if (node.SwitchContinuation is { } continuation)
            {
                AnalyzeNode(
                    context,
                    continuation,
                    paths,
                    result,
                    cancellationToken);
            }

            return;
        }

        if (node.Condition is null)
        {
            return;
        }

        AnalyzeNode(
            context,
            node.WhenTrue!,
            paths,
            result,
            cancellationToken);
        AnalyzeNode(
            context,
            node.WhenFalse!,
            paths,
            result,
            cancellationToken);
    }

    private static void AnalyzeFailure(
        ConstructionAnalysisContext context,
        TypeMapperMappingModel mapping,
        MappingFailureObservation? failure,
        MappingExecutionPathSet reachablePaths,
        ImmutableArray<ConstructionDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        if (failure is null ||
            reachablePaths == MappingExecutionPathSet.None)
        {
            return;
        }

        var affectedPaths = reachablePaths & failure.AffectedPath.Paths;

        if (affectedPaths == MappingExecutionPathSet.None)
        {
            return;
        }

        var policy = FindResultPolicy(context.Pair, failure);
        var semanticModel = policy?.Expression.SemanticModel;

        if (semanticModel is not null &&
            failure.OffendingNode is { } offendingNode &&
            HasSourceBindingError(
                offendingNode,
                semanticModel,
                cancellationToken))
        {
            return;
        }

        switch (failure.Reason)
        {
            case MappingFailureReason.MissingConstructionPolicy:
                result.Add(BuildMissingConstructionCandidate(
                    context,
                    failure,
                    affectedPaths));
                return;

            case MappingFailureReason.ConstructorSelectionFailed:
                AddConstructorPlanningCandidates(
                    context,
                    mapping,
                    failure,
                    policy,
                    affectedPaths,
                    result,
                    cancellationToken);
                return;

            case MappingFailureReason.ConstructorParameterRuleInvalid:
                AddParameterRuleCandidates(
                    context,
                    mapping,
                    failure,
                    policy,
                    affectedPaths,
                    result,
                    cancellationToken,
                    suppressConventionFallback: false);
                return;

            case MappingFailureReason.TerminalPreviousWithoutValue:
                result.Add(BuildTerminalCandidate(
                    context,
                    mapping,
                    failure,
                    policy,
                    affectedPaths,
                    StructuredTerminalKind.Previous));
                return;

            case MappingFailureReason.TerminalNullConstruction:
                result.Add(BuildTerminalCandidate(
                    context,
                    mapping,
                    failure,
                    policy,
                    affectedPaths,
                    StructuredTerminalKind.NullConstruction));
                return;
        }
    }

    private static ConstructionDiagnosticCandidate
        BuildMissingConstructionCandidate(
            ConstructionAnalysisContext context,
            MappingFailureObservation failure,
            MappingExecutionPathSet paths)
    {
        var registration = context.Pair.Origin.DeclaredRegistration;
        var primary = GetInvocationNameLocation(registration.Syntax);
        var contract = MapperContractDisplay.Create(
            registration.SourceType,
            registration.DestinationType);

        return CreateCandidate(
            ConstructionDiagnosticKind.MissingConstructionPolicy,
            context,
            failure,
            primary,
            additionalLocations: ImmutableArray<Location>.Empty,
            detail: string.Empty,
            contract,
            parameterName: string.Empty,
            strategy: string.Empty,
            reason: string.Empty,
            paths);
    }

    private static void AddConstructorPlanningCandidates(
        ConstructionAnalysisContext context,
        TypeMapperMappingModel mapping,
        MappingFailureObservation failure,
        ResultPolicyConfigurationModel? policy,
        MappingExecutionPathSet paths,
        ImmutableArray<ConstructionDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        if (mapping.ConstructorObservation is not { } observation ||
            observation.Strategy is not { } strategy)
        {
            return;
        }

        if (AddParameterRuleCandidates(
                context,
                mapping,
                failure,
                policy,
                paths,
                result,
                cancellationToken,
                suppressConventionFallback: true))
        {
            return;
        }

        if (!TryBuildConventionReason(
                observation,
                strategy,
                out var reason,
                out var reasonKind,
                out var selectedConstructor))
        {
            return;
        }

        var primary = observation.StrategyOrigin is { } strategyOrigin &&
                      ContainsInvocationName(
                          strategyOrigin,
                          "ByConvention")
            ? GetMarkerNameLocation(strategyOrigin, "ByConvention")
            : GetInvocationNameLocation(
                mapping.AnalysisContext.Registration.Syntax);
        var additional = ImmutableArray.CreateBuilder<Location>();
        var settingOrigin = GetEffectiveConstructorSettingOrigin(
            context.Mapper,
            context.Pair);

        if (settingOrigin is not null)
        {
            AddDistinct(additional, settingOrigin.GetLocation(), primary);
        }

        if (selectedConstructor is not null &&
            GetConstructorDeclarationLocation(
                selectedConstructor,
                cancellationToken) is { } constructorLocation)
        {
            AddDistinct(additional, constructorLocation, primary);
        }

        var contract = GetContract(context.Pair, policy);

        result.Add(CreateCandidate(
            ConstructionDiagnosticKind.ConventionUnavailable,
            context,
            failure,
            primary,
            additional.ToImmutable(),
            reasonKind,
            contract,
            parameterName: string.Empty,
            strategy.ToString(),
            reason,
            paths));
    }

    private static bool AddParameterRuleCandidates(
        ConstructionAnalysisContext context,
        TypeMapperMappingModel mapping,
        MappingFailureObservation failure,
        ResultPolicyConfigurationModel? policy,
        MappingExecutionPathSet paths,
        ImmutableArray<ConstructionDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken,
        bool suppressConventionFallback)
    {
        if (mapping.ConstructorObservation is not { } observation ||
            policy is not { } resolvedPolicy)
        {
            return false;
        }

        var rules = FindInvalidRules(
            observation,
            suppressConventionFallback);

        if (rules.IsEmpty)
        {
            return false;
        }

        var contract = GetContract(context.Pair, resolvedPolicy);
        var added = false;

        foreach (var item in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryBuildParameterRuleDiagnostic(
                    item.Rule,
                    item.Constructor,
                    resolvedPolicy.Expression.SemanticModel,
                    mapping.AnalysisContext.TargetMapper,
                    cancellationToken,
                    out var diagnostic))
            {
                continue;
            }

            if (HasSourceBindingError(
                    item.Rule.OriginNode ?? item.Rule.DesignatorNode,
                    resolvedPolicy.Expression.SemanticModel,
                    cancellationToken))
            {
                continue;
            }

            var additional = ImmutableArray.CreateBuilder<Location>();

            if (GetConstructorDeclarationLocation(
                    item.Constructor,
                    cancellationToken) is { } constructorLocation)
            {
                AddDistinct(
                    additional,
                    constructorLocation,
                    diagnostic.PrimaryLocation);
            }

            result.Add(CreateCandidate(
                ConstructionDiagnosticKind.InvalidParameterRule,
                context,
                failure,
                diagnostic.PrimaryLocation,
                additional.ToImmutable(),
                diagnostic.ReasonKind,
                contract,
                diagnostic.ParameterName,
                strategy: string.Empty,
                diagnostic.Reason,
                paths));
            added = true;
        }

        return added;
    }

    private static ImmutableArray<InvalidRuleCandidate> FindInvalidRules(
        ConstructorPlanningObservation observation,
        bool requireCompleteAttribution)
    {
        if (observation.SelectedConstructor is { } selectedConstructor)
        {
            var selected = observation.Candidates.FirstOrDefault(candidate =>
                ConventionConstructorMappingPlanner.AreSameConstructor(
                    candidate.Constructor,
                    selectedConstructor));

            if (selected is null ||
                selected.RejectionReason is
                    ConstructorCandidateRejectionReason.RequiredMember or
                    ConstructorCandidateRejectionReason
                        .ResultDependentInitializer)
            {
                return ImmutableArray<InvalidRuleCandidate>.Empty;
            }

            var invalid = selected.ParameterRules
                .Where(IsExplicitInvalidRule)
                .Select(rule => new InvalidRuleCandidate(
                    selected.Constructor,
                    rule))
                .ToImmutableArray();

            if (!invalid.IsEmpty)
            {
                return invalid;
            }

            if (selected.RejectionReason ==
                    ConstructorCandidateRejectionReason.InvocationBinding)
            {
                var explicitRules = selected.ParameterRules
                    .Where(IsExplicitRule)
                    .ToImmutableArray();

                if (explicitRules.Length == 1)
                {
                    return ImmutableArray.Create<InvalidRuleCandidate>(new InvalidRuleCandidate(
                        selected.Constructor,
                        explicitRules[0] with
                        {
                            IsApplicable = false,
                            RejectionReason =
                                ConstructorCandidateRejectionReason
                                    .InvocationBinding
                        }));
                }
            }

            return ImmutableArray<InvalidRuleCandidate>.Empty;
        }

        if (!requireCompleteAttribution ||
            observation.Strategy != ConstructorSelectionValue.Greediest ||
            observation.Candidates.IsEmpty)
        {
            return ImmutableArray<InvalidRuleCandidate>.Empty;
        }

        var groups = observation.Candidates
            .SelectMany(candidate => candidate.ParameterRules
                .Where(rule => rule.OriginNode is not null &&
                               (IsExplicitInvalidRule(rule) ||
                                candidate.RejectionReason ==
                                    ConstructorCandidateRejectionReason
                                        .InvocationBinding &&
                                candidate.ParameterRules.Count(
                                    IsExplicitRule) == 1))
                .Select(rule => new InvalidRuleCandidate(
                    candidate.Constructor,
                    rule)))
            .GroupBy(candidate => LocationIdentity(
                (candidate.Rule.OriginNode ??
                 candidate.Rule.DesignatorNode)!.GetLocation()),
                StringComparer.Ordinal);
        var result = ImmutableArray.CreateBuilder<InvalidRuleCandidate>();

        foreach (var group in groups)
        {
            if (group.Select(static candidate =>
                    ConstructorIdentity(candidate.Constructor))
                .Distinct(StringComparer.Ordinal)
                .Count() != observation.Candidates.Length)
            {
                continue;
            }

            var first = group.First();
            result.Add(first with
            {
                Rule = first.Rule with
                {
                    IsApplicable = false,
                    RejectionReason = first.Rule.RejectionReason ==
                        ConstructorCandidateRejectionReason.None
                            ? ConstructorCandidateRejectionReason
                                .InvocationBinding
                            : first.Rule.RejectionReason
                }
            });
        }

        return result.ToImmutable();
    }

    private static bool TryBuildParameterRuleDiagnostic(
        ConstructorParameterRuleObservation rule,
        IMethodSymbol constructor,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken,
        out ParameterRuleDiagnostic diagnostic)
    {
        var parameterName = rule.Parameter?.Name ?? rule.ParameterName;
        var origin = rule.OriginNode ?? rule.DesignatorNode;

        if (origin is null)
        {
            diagnostic = default;
            return false;
        }

        if (rule.Parameter is { } parameter &&
            TryGetTypedMarkerMismatch(
                origin,
                parameter,
                semanticModel,
                mapperType,
                cancellationToken,
                out var actualType,
                out var parameterType,
                out var markerLocation))
        {
            diagnostic = new ParameterRuleDiagnostic(
                parameterName,
                markerLocation,
                "marker-type",
                $"specified type '{actualType}' does not match " +
                $"parameter type '{parameterType}'");
            return true;
        }

        if (rule.Parameter is null)
        {
            diagnostic = new ParameterRuleDiagnostic(
                parameterName,
                (rule.DesignatorNode ?? origin).GetLocation(),
                "missing-parameter",
                $"selected constructor '{DisplayConstructor(constructor)}' " +
                "does not declare this parameter");
            return true;
        }

        if (rule.Origin == ConstructorParameterRuleOrigin.Auto)
        {
            diagnostic = new ParameterRuleDiagnostic(
                parameterName,
                GetMarkerNameLocation(origin, "Auto"),
                "auto",
                "Auto could not find exactly one compatible source member");
            return true;
        }

        if (rule.Origin == ConstructorParameterRuleOrigin.Ignore)
        {
            diagnostic = new ParameterRuleDiagnostic(
                parameterName,
                GetMarkerNameLocation(origin, "Ignore"),
                "ignore",
                "Ignore can only omit an optional or params parameter");
            return true;
        }

        if (TryGetMarkerInvocation(
                origin,
                semanticModel,
                cancellationToken,
                out var markerInvocation,
                out var markerKind) &&
            markerKind is DeclarativeIntrinsicKind.Auto or
                DeclarativeIntrinsicKind.Ignore or
                DeclarativeIntrinsicKind.Value)
        {
            diagnostic = new ParameterRuleDiagnostic(
                parameterName,
                GetInvocationNameLocation(markerInvocation),
                "binding",
                $"this rule cannot be used with constructor " +
                $"'{DisplayConstructor(constructor)}'");
            return true;
        }

        diagnostic = new ParameterRuleDiagnostic(
            parameterName,
            origin.GetLocation(),
            "binding",
            $"this rule cannot be used with constructor " +
            $"'{DisplayConstructor(constructor)}'");
        return true;
    }

    private static bool TryGetTypedMarkerMismatch(
        SyntaxNode origin,
        IParameterSymbol parameter,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken,
        out string actualType,
        out string parameterType,
        out Location markerLocation)
    {
        if (!TryGetMarkerInvocation(
                origin,
                semanticModel,
                cancellationToken,
                out var invocation,
                out var kind) ||
            kind is not (DeclarativeIntrinsicKind.Auto or
                DeclarativeIntrinsicKind.Ignore or
                DeclarativeIntrinsicKind.Value) ||
            semanticModel.GetOperation(
                invocation,
                cancellationToken) is not IInvocationOperation
            {
                TargetMethod:
                {
                    IsGenericMethod: true,
                    TypeArguments.Length: 1
                } method
        })
        {
            actualType = string.Empty;
            parameterType = string.Empty;
            markerLocation = Location.None;
            return false;
        }

        var assertedType = method.TypeArguments[0]
            .WithNullableAnnotation(
                method.TypeArgumentNullableAnnotations[0]);
        var targetType = parameter.Type.WithNullableAnnotation(
            parameter.NullableAnnotation);

        if (DeclarativeIntrinsic.HasExactTargetType(
                assertedType,
                targetType,
                semanticModel,
                mapperType))
        {
            actualType = string.Empty;
            parameterType = string.Empty;
            markerLocation = Location.None;
            return false;
        }

        actualType = MapperContractDisplay.CreateType(assertedType);
        parameterType = MapperContractDisplay.CreateType(targetType);
        markerLocation = GetInvocationNameLocation(invocation);
        return true;
    }

    private static bool TryGetMarkerInvocation(
        SyntaxNode origin,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out InvocationExpressionSyntax invocation,
        out DeclarativeIntrinsicKind kind)
    {
        foreach (var candidate in origin.DescendantNodesAndSelf()
                     .OfType<InvocationExpressionSyntax>())
        {
            if (DeclarativeIntrinsic.TryGetKind(
                    candidate,
                    semanticModel,
                    cancellationToken,
                    out kind,
                    out _))
            {
                invocation = candidate;
                return true;
            }
        }

        invocation = null!;
        kind = default;
        return false;
    }

    private static bool TryBuildConventionReason(
        ConstructorPlanningObservation observation,
        ConstructorSelectionValue strategy,
        out string reason,
        out string reasonKind,
        out IMethodSymbol? selectedConstructor)
    {
        selectedConstructor = observation.SelectedConstructor;
        var constructors = observation.Candidates;

        switch (strategy)
        {
            case ConstructorSelectionValue.Explicit:
                reason = "destination construction must be configured " +
                    "explicitly";
                reasonKind = "explicit";
                return true;

            case ConstructorSelectionValue.Parameterless
                when !constructors.Any(static candidate =>
                    candidate.Constructor.Parameters.IsEmpty):
                reason =
                    "no supported parameterless constructor is available";
                reasonKind = "parameterless";
                return true;

            case ConstructorSelectionValue.Single
                when constructors.Length != 1:
                reason = "exactly one supported constructor is required, " +
                    $"but {constructors.Length.ToString(CultureInfo.InvariantCulture)} " +
                    "were found";
                reasonKind = "single-count";
                return true;

            case ConstructorSelectionValue.Unambiguous
                when constructors.Count(static candidate =>
                    !candidate.Constructor.Parameters.IsEmpty) > 1:
                reason =
                    "more than one supported parameterized constructor is " +
                    "available";
                reasonKind = "unambiguous";
                return true;

            case ConstructorSelectionValue.Largest
                when HasLargestTie(constructors):
                reason = "multiple supported constructors have the largest " +
                    "declared parameter count";
                reasonKind = "largest-tie";
                selectedConstructor = null;
                return true;

            case ConstructorSelectionValue.Greediest:
            {
                var applicable = constructors.Where(static candidate =>
                        candidate.RejectionReason ==
                            ConstructorCandidateRejectionReason.None)
                    .ToImmutableArray();

                if (applicable.IsEmpty)
                {
                    reason = "no constructor can be called with " +
                        "automatically mapped arguments";
                    reasonKind = "greediest-no-plan";
                    selectedConstructor = null;
                    return true;
                }

                reason = "multiple constructors accept the same highest " +
                    "number of mapped arguments";
                reasonKind = "greediest-tie";
                selectedConstructor = null;
                return true;
            }
        }

        if (selectedConstructor is null)
        {
            reason = "no constructor can be called with automatically " +
                "mapped arguments";
            reasonKind = "no-plan";
            return true;
        }

        var selectedConstructorValue = selectedConstructor;
        var selected = constructors.FirstOrDefault(candidate =>
            ConventionConstructorMappingPlanner.AreSameConstructor(
                candidate.Constructor,
                selectedConstructorValue));

        if (selected is null ||
            selected.RejectionReason is
                ConstructorCandidateRejectionReason.RequiredMember or
                ConstructorCandidateRejectionReason.ResultDependentInitializer)
        {
            reason = string.Empty;
            reasonKind = string.Empty;
            return false;
        }

        var blockingParameter = selected.ParameterRules.FirstOrDefault(rule =>
            !rule.IsApplicable &&
            rule.Origin is
                ConstructorParameterRuleOrigin.Convention or
                ConstructorParameterRuleOrigin.Omitted);

        if (blockingParameter is not null &&
            selected.RejectionReason is
                ConstructorCandidateRejectionReason.MissingSourceMember or
                ConstructorCandidateRejectionReason.IncompatibleArgument)
        {
            reason = $"constructor " +
                $"'{DisplayConstructor(selectedConstructor)}' has no " +
                "compatible source member for required parameter " +
                $"'{blockingParameter.ParameterName}'";
            reasonKind = "parameter";
            return true;
        }

        reason = $"constructor " +
            $"'{DisplayConstructor(selectedConstructor)}' cannot be called " +
            "with the mapped arguments";
        reasonKind = "binding";
        return true;
    }

    private static bool HasLargestTie(
        ImmutableArray<ConstructorCandidateObservation> constructors)
    {
        if (constructors.Length < 2)
        {
            return false;
        }

        var maximum = constructors.Max(static candidate =>
            candidate.Constructor.Parameters.Length);

        return constructors.Count(candidate =>
            candidate.Constructor.Parameters.Length == maximum) > 1;
    }

    private static ConstructionDiagnosticCandidate BuildTerminalCandidate(
        ConstructionAnalysisContext context,
        TypeMapperMappingModel mapping,
        MappingFailureObservation failure,
        ResultPolicyConfigurationModel? policy,
        MappingExecutionPathSet paths,
        StructuredTerminalKind kind)
    {
        var terminal = FindTerminal(mapping, failure, kind);
        var aliases = terminal?.Aliases.IsDefault == false
            ? terminal.Aliases
            : ImmutableArray<DeclarativeTerminalAliasSyntax>.Empty;
        Location primary;
        var additional = ImmutableArray.CreateBuilder<Location>();

        if (kind == StructuredTerminalKind.Previous && !aliases.IsEmpty)
        {
            primary = aliases[aliases.Length - 1].Use.GetLocation();
            AddDistinct(
                additional,
                (terminal?.OriginNode ?? failure.OriginNode).GetLocation(),
                primary);

            foreach (var alias in aliases.Take(aliases.Length - 1))
            {
                AddDistinct(additional, alias.Use.GetLocation(), primary);
            }
        }
        else
        {
            primary = (terminal?.OriginNode ??
                       failure.OffendingNode ??
                       failure.OriginNode).GetLocation();

            foreach (var alias in aliases)
            {
                AddDistinct(additional, alias.Use.GetLocation(), primary);
            }
        }

        var orderedAdditional = additional
            .OrderBy(LocationPath, StringComparer.Ordinal)
            .ThenBy(static location => location.SourceSpan.Start)
            .ToImmutableArray();
        var contract = GetContract(context.Pair, policy);
        var diagnosticKind = kind == StructuredTerminalKind.Previous
            ? ConstructionDiagnosticKind.PreviousUnavailable
            : ConstructionDiagnosticKind.NullConstructionPlan;

        return CreateCandidate(
            diagnosticKind,
            context,
            failure,
            primary,
            orderedAdditional,
            detail: LocationIdentity(primary),
            contract,
            parameterName: string.Empty,
            strategy: string.Empty,
            reason: string.Empty,
            paths);
    }

    private static StructuredTerminalObservation? FindTerminal(
        TypeMapperMappingModel mapping,
        MappingFailureObservation failure,
        StructuredTerminalKind kind)
    {
        var candidates = (mapping.StructuredTerminals.IsDefault
                ? ImmutableArray<StructuredTerminalObservation>.Empty
                : mapping.StructuredTerminals)
            .AddRange(mapping.ConstructorObservation?.Terminals.IsDefault == false
                ? mapping.ConstructorObservation.Terminals
                : ImmutableArray<StructuredTerminalObservation>.Empty);

        return candidates.FirstOrDefault(terminal =>
            terminal.Kind == kind &&
            SameSyntax(terminal.OriginNode, failure.OriginNode));
    }

    private static ConstructionDiagnosticCandidate CreateCandidate(
        ConstructionDiagnosticKind kind,
        ConstructionAnalysisContext context,
        MappingFailureObservation failure,
        Location primary,
        ImmutableArray<Location> additionalLocations,
        string detail,
        string contract,
        string parameterName,
        string strategy,
        string reason,
        MappingExecutionPathSet paths)
    {
        var sourceMapper = failure.OriginKind is
                MappingObservationOriginKind.Registration or
                MappingObservationOriginKind.Convention
            ? context.Pair.Origin.DeclaringMapperType.OriginalDefinition
            : failure.SourceMapper.OriginalDefinition;
        var mapperIdentity = SymbolNameHelper.GetFullMetadataName(sourceMapper);
        var originIdentity = LocationIdentity(primary);
        var idOrder = (int)kind;
        var registration = context.Pair.Origin.DeclaredRegistration;
        var pairKey = MappingTypeIdentityPolicy.Create(
                registration.SourceType).Key +
            "->" + MappingTypeIdentityPolicy.Create(
                registration.DestinationType).Key;
        var identity = idOrder.ToString(CultureInfo.InvariantCulture) + "|" +
            mapperIdentity + "|" + pairKey + "|" + originIdentity + "|" +
            detail;

        return new ConstructionDiagnosticCandidate(
            kind,
            identity,
            mapperIdentity,
            context.Pair.Origin.LevelOrder,
            pairKey,
            originIdentity,
            primary.SourceSpan.Start,
            detail,
            SymbolEqualityComparer.Default.Equals(
                context.Pair.Origin.DeclaringMapperType.OriginalDefinition,
                sourceMapper),
            primary,
            failure.OriginNode.GetLocation(),
            additionalLocations,
            contract,
            parameterName,
            strategy,
            reason,
            paths);
    }

    private static MappingExecutionPathSet GetReachablePaths(
        TypeMapperMappingModel mapping)
    {
        var settings = mapping.EffectiveSettings;
        var result = MappingExecutionPathSet.None;

        if (settings.SupportsCreate &&
            mapping.CreateOperationFailure is null)
        {
            result |= MappingExecutionPathSet.Create;
        }

        if (settings.SupportsUpdate &&
            mapping.UpdateOperationFailure is null)
        {
            result |= MappingExecutionPathSet.UpdateWithPrevious;

            if (mapping.DestinationCanBeNull &&
                settings.NullDestinationHandling ==
                    NullDestinationHandlingValue.Create)
            {
                result |= MappingExecutionPathSet.UpdateWithoutPrevious;
            }
        }

        return result;
    }

    private static bool CanAnalyze(TypeMapperMappingModel mapping)
    {
        if (mapping.ManualMapping is not null ||
            mapping.Failure is { } failure &&
            IsPriorCategoryFailure(failure.Reason))
        {
            return false;
        }

        return mapping.EffectiveSettings.HasExecutableOperation;
    }

    private static bool IsPriorCategoryFailure(MappingFailureReason reason)
    {
        return reason is
            MappingFailureReason.UnsupportedMappingContract or
            MappingFailureReason.InvalidBaseConfiguration or
            MappingFailureReason.UnsupportedMapperBuilderFlow or
            MappingFailureReason.UnsupportedMappingBuilderFlow or
            MappingFailureReason.InvalidPairConfiguration or
            MappingFailureReason.InvalidManualSetting or
            MappingFailureReason.InvalidSetting or
            MappingFailureReason.InapplicableSetting or
            MappingFailureReason.CallbackCannotBeTransferred or
            MappingFailureReason.UnsupportedRuntimeCallback or
            MappingFailureReason.UnsupportedStructuredCallback or
            MappingFailureReason.UnsupportedStructuredSyntax or
            MappingFailureReason.StructuredResultRequiresDestination;
    }

    private static ResultPolicyConfigurationModel? FindResultPolicy(
        PairConfigurationModel pair,
        MappingFailureObservation failure)
    {
        foreach (var policy in pair.Declarative.ResultPolicies)
        {
            if (Contains(policy.Expression.Syntax, failure.OriginNode) ||
                failure.OffendingNode is { } offending &&
                Contains(policy.Expression.Syntax, offending))
            {
                return policy;
            }
        }

        return null;
    }

    private static string GetContract(
        PairConfigurationModel pair,
        ResultPolicyConfigurationModel? policy)
    {
        if (policy is { } resolvedPolicy &&
            resolvedPolicy.Invocation.Expression is
                MemberAccessExpressionSyntax memberAccess &&
            resolvedPolicy.Expression.SemanticModel.GetTypeInfo(
                    memberAccess.Expression)
                .Type is INamedTypeSymbol
                {
                    TypeArguments.Length: 2
                } builderType &&
            StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    builderType.OriginalDefinition),
                "Morphant.MapperBuilder`2"))
        {
            return MapperContractDisplay.Create(
                builderType.TypeArguments[0],
                builderType.TypeArguments[1]);
        }

        return MapperContractDisplay.Create(
            pair.Origin.DeclaredRegistration.SourceType,
            pair.Origin.DeclaredRegistration.DestinationType);
    }

    private static SyntaxNode? GetEffectiveConstructorSettingOrigin(
        MapperPairConfigurationModel mapper,
        PairConfigurationModel pair)
    {
        SyntaxNode? Resolve(PairConfigurationSettings settings)
        {
            var value = settings.ConstructorSelection;

            if (value.Origin == PairConfigurationSettingOrigin.Unset ||
                value.Value == ConstructorSelectionValue.Default)
            {
                return null;
            }

            return GetSettingArgument(value.Syntax);
        }

        if (Resolve(pair.Settings) is { } local)
        {
            return local;
        }

        foreach (var settings in pair.Composition.IncludedBaseSettings)
        {
            if (Resolve(settings) is { } included)
            {
                return included;
            }
        }

        if (Resolve(mapper.RootSettings) is { } root)
        {
            return root;
        }

        foreach (var settings in mapper.BaseRootSettings)
        {
            if (Resolve(settings) is { } baseRoot)
            {
                return baseRoot;
            }
        }

        return null;
    }

    private static SyntaxNode? GetSettingArgument(SyntaxNode? syntax)
    {
        return syntax is InvocationExpressionSyntax invocation &&
               invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression
                   is { } expression
            ? expression
            : syntax;
    }

    private static bool TryGetMapping(
        TypeMapperModel model,
        MappingPairIdentity identity,
        out TypeMapperMappingModel mapping)
    {
        foreach (var candidate in model.Mappings)
        {
            if (StringComparer.Ordinal.Equals(
                    candidate.AnalysisContext.Identity.Source.Key,
                    identity.Source.Key) &&
                StringComparer.Ordinal.Equals(
                    candidate.AnalysisContext.Identity.Destination.Key,
                    identity.Destination.Key))
            {
                mapping = candidate;
                return true;
            }
        }

        mapping = default;
        return false;
    }

    private static bool HasSourceBindingError(
        SyntaxNode? node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return node is not null &&
               semanticModel.GetDiagnostics(
                       node.Span,
                       cancellationToken)
                   .Any(static diagnostic =>
                       diagnostic.Severity == DiagnosticSeverity.Error);
    }

    private static Location GetInvocationNameLocation(
        InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax genericName
            } => genericName.Identifier.GetLocation(),
            MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Name.GetLocation(),
            GenericNameSyntax genericName =>
                genericName.Identifier.GetLocation(),
            SimpleNameSyntax simpleName => simpleName.GetLocation(),
            _ => invocation.Expression.GetLocation()
        };
    }

    private static bool ContainsInvocationName(
        SyntaxNode origin,
        string name)
    {
        return origin.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => invocation.Expression switch
            {
                MemberAccessExpressionSyntax access =>
                    access.Name.Identifier.ValueText == name,
                SimpleNameSyntax simple =>
                    simple.Identifier.ValueText == name,
                _ => false
            });
    }

    private static Location GetMarkerNameLocation(
        SyntaxNode origin,
        string markerName)
    {
        var invocation = origin.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(candidate =>
                candidate.Expression switch
                {
                    MemberAccessExpressionSyntax access =>
                        access.Name.Identifier.ValueText == markerName,
                    SimpleNameSyntax simple =>
                        simple.Identifier.ValueText == markerName,
                    _ => false
                });

        return invocation is null
            ? origin.GetLocation()
            : GetInvocationNameLocation(invocation);
    }

    private static Location? GetConstructorDeclarationLocation(
        IMethodSymbol constructor,
        CancellationToken cancellationToken)
    {
        foreach (var reference in constructor.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is
                ConstructorDeclarationSyntax declaration)
            {
                return declaration.Identifier.GetLocation();
            }
        }

        return null;
    }

    private static string DisplayConstructor(IMethodSymbol constructor)
    {
        var containingType = MapperContractDisplay.CreateType(
            constructor.ContainingType);
        var parameters = constructor.Parameters.Select(parameter =>
            MapperContractDisplay.CreateType(
                parameter.Type.WithNullableAnnotation(
                    parameter.NullableAnnotation)) +
            " " + parameter.Name);

        return containingType + "(" + string.Join(", ", parameters) + ")";
    }

    private static string ConstructorIdentity(IMethodSymbol constructor)
    {
        return constructor.GetDocumentationCommentId() ??
               DisplayConstructor(constructor);
    }

    private static bool IsExplicitRule(
        ConstructorParameterRuleObservation rule)
    {
        return rule.OriginNode is not null &&
               rule.Origin is
                   ConstructorParameterRuleOrigin.Auto or
                   ConstructorParameterRuleOrigin.Ignore or
                   ConstructorParameterRuleOrigin.Value;
    }

    private static bool IsExplicitInvalidRule(
        ConstructorParameterRuleObservation rule)
    {
        return IsExplicitRule(rule) && !rule.IsApplicable;
    }

    private static void AddDistinct(
        ImmutableArray<Location>.Builder locations,
        Location location,
        Location primary)
    {
        if (!SameLocation(location, primary) &&
            !locations.Any(candidate => SameLocation(candidate, location)))
        {
            locations.Add(location);
        }
    }

    private static bool Contains(SyntaxNode container, SyntaxNode node)
    {
        return ReferenceEquals(container.SyntaxTree, node.SyntaxTree) &&
               container.FullSpan.Contains(node.FullSpan);
    }

    private static bool SameSyntax(SyntaxNode left, SyntaxNode right)
    {
        return ReferenceEquals(left.SyntaxTree, right.SyntaxTree) &&
               left.Span == right.Span;
    }

    private static bool SameLocation(Location left, Location right)
    {
        return ReferenceEquals(left.SourceTree, right.SourceTree) &&
               left.SourceSpan == right.SourceSpan;
    }

    private static string LocationIdentity(Location location)
    {
        return LocationPath(location) + "|" +
               location.SourceSpan.Start + "|" +
               location.SourceSpan.Length;
    }

    private static string LocationPath(Location location) =>
        location.SourceTree?.FilePath ?? string.Empty;

    internal static string FormatPaths(MappingExecutionPathSet paths)
    {
        var names = ImmutableArray.CreateBuilder<string>();

        if (paths.HasFlag(MappingExecutionPathSet.Create))
        {
            names.Add("Create");
        }

        if (paths.HasFlag(MappingExecutionPathSet.UpdateWithoutPrevious))
        {
            names.Add("Update without an existing destination");
        }

        if (paths.HasFlag(MappingExecutionPathSet.UpdateWithPrevious))
        {
            names.Add("Update with an existing destination");
        }

        return string.Join("; ", names);
    }

    private readonly record struct ConstructionAnalysisContext(
        MapperPairConfigurationModel Mapper,
        PairConfigurationModel Pair,
        TypeMapperMappingModel Mapping);

    private readonly record struct InvalidRuleCandidate(
        IMethodSymbol Constructor,
        ConstructorParameterRuleObservation Rule);

    private readonly record struct ParameterRuleDiagnostic(
        string ParameterName,
        Location PrimaryLocation,
        string ReasonKind,
        string Reason);
}
