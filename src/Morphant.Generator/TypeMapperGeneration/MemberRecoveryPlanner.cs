using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MemberRecoveryPlanner
{
    private const string InvalidMemberRuleMessage =
        "This member rule is invalid.";

    private const string NullMembersMessage =
        "Members returned null or default.";

    private const string MemberLifecycleMessage =
        "This member cannot be assigned in this Create or Update case.";

    public static TypeMapperMappingModel Apply(
        TypeMapperMappingModel mapping,
        ConventionMemberMappingPlan plan,
        ResultPolicyKind? resultPolicy,
        INamedTypeSymbol mapperType)
    {
        if (mapping.Failure is not null ||
            !HasIntrinsicFailure(plan) &&
            !HasRuntimeLifecycleFailure(plan, resultPolicy))
        {
            return mapping;
        }

        if (mapping.ControlFlow is { } controlFlow)
        {
            return mapping with
            {
                ControlFlow = new TypeMapperControlFlowMappingModel(
                    ApplyNode(
                        controlFlow.CreateRoot,
                        plan,
                        resultPolicy,
                        mapperType,
                        MappingExecutionPathSet.NoPrevious,
                        createRoot: true),
                    ApplyNode(
                        controlFlow.UpdateRoot,
                        plan,
                        resultPolicy,
                        mapperType,
                        MappingExecutionPathSet.UpdateWithPrevious,
                        createRoot: false))
            };
        }

        var result = mapping;

        if (TryBuildFailure(
                result,
                plan,
                resultPolicy,
                MappingExecutionPathSet.NoPrevious,
                existingDestination: false,
                runtimeResult: result.CreateFactory is not null,
                out var createFailure) &&
            CanApplyFailure(result.CreateFailure, createFailure))
        {
            result = result.CreateFactory is not null
                ? ApplyPostResultFailure(
                    result,
                    createFailure,
                    mapperType)
                : result with { CreateFailure = createFailure };
        }

        if (TryBuildFailure(
                result,
                plan,
                resultPolicy,
                MappingExecutionPathSet.UpdateWithPrevious,
                existingDestination: true,
                runtimeResult: false,
                out var updateFailure) &&
            CanApplyFailure(result.UpdateFailure, updateFailure))
        {
            result = result with { UpdateFailure = updateFailure };
        }

        return result;
    }

    private static TypeMapperControlFlowNode ApplyNode(
        TypeMapperControlFlowNode node,
        ConventionMemberMappingPlan plan,
        ResultPolicyKind? resultPolicy,
        INamedTypeSymbol mapperType,
        MappingExecutionPathSet paths,
        bool createRoot)
    {
        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            return node with
            {
                EvaluationContinuation = ApplyNode(
                    evaluationContinuation,
                    plan,
                    resultPolicy,
                    mapperType,
                    paths,
                    createRoot)
            };
        }

        if (node.SwitchExpression is not null)
        {
            return node with
            {
                SwitchSections = node.SwitchSections.Select(section =>
                        section with
                        {
                            Branch = ApplyNode(
                                section.Branch,
                                plan,
                                resultPolicy,
                                mapperType,
                                paths,
                                createRoot)
                        })
                    .ToImmutableArray(),
                SwitchContinuation = node.SwitchContinuation is
                    { } continuation
                        ? ApplyNode(
                            continuation,
                            plan,
                            resultPolicy,
                            mapperType,
                            paths,
                            createRoot)
                        : null
            };
        }

        if (node.Condition is not null)
        {
            return node with
            {
                WhenTrue = ApplyNode(
                    node.WhenTrue!,
                    plan,
                    resultPolicy,
                    mapperType,
                    paths,
                    createRoot),
                WhenFalse = ApplyNode(
                    node.WhenFalse!,
                    plan,
                    resultPolicy,
                    mapperType,
                    paths,
                    createRoot)
            };
        }

        if (node.Leaf is not { } leaf)
        {
            return node;
        }

        var replacement = !createRoot &&
            (leaf.CreateFactory is not null ||
             leaf.CreateConstructor is not null);
        var existingDestination = !createRoot && !replacement;
        var runtimeResult = leaf.CreateFactory is not null &&
            resultPolicy is
                ResultPolicyKind.ConstructUsing or
                ResultPolicyKind.ResolveUsing;

        if (!TryBuildFailure(
                leaf,
                plan,
                resultPolicy,
                paths,
                existingDestination,
                runtimeResult,
                out var failure))
        {
            return node;
        }

        var currentFailure = createRoot
            ? leaf.CreateFailure
            : leaf.UpdateFailure;

        if (!CanApplyFailure(currentFailure, failure))
        {
            return node;
        }

        if (runtimeResult)
        {
            return node with
            {
                Leaf = ApplyPostResultFailure(
                    leaf,
                    failure,
                    mapperType)
            };
        }

        return node with
        {
            Leaf = createRoot
                ? leaf with { CreateFailure = failure }
                : leaf with { UpdateFailure = failure }
        };
    }

    internal static bool TryBuildFailure(
        TypeMapperMappingModel mapping,
        ConventionMemberMappingPlan plan,
        ResultPolicyKind? resultPolicy,
        MappingExecutionPathSet paths,
        bool existingDestination,
        bool runtimeResult,
        out MappingFailureObservation failure)
    {
        var invalidRule = plan.Observation.Rules.FirstOrDefault(rule =>
            rule.InvalidReason != MemberRuleInvalidReason.None &&
            AppliesTo(rule, existingDestination));

        if (invalidRule is not null)
        {
            failure = BuildFailure(
                mapping,
                invalidRule.SourceMapper,
                MappingFailureReason.MemberRuleInvalid,
                InvalidMemberRuleMessage,
                invalidRule.OriginNode ??
                invalidRule.DesignatorNode ??
                mapping.AnalysisContext.Registration.Syntax,
                paths);
            return true;
        }

        var terminal = plan.Observation.Terminals.FirstOrDefault(
            static candidate =>
                candidate.Kind == StructuredTerminalKind.NullMembers);

        if (terminal is not null)
        {
            failure = plan.Failure ?? BuildFailure(
                mapping,
                sourceMapper: null,
                MappingFailureReason.TerminalNullMembers,
                NullMembersMessage,
                terminal.OriginNode,
                paths);
            failure = failure with
            {
                AffectedPath = new MappingAffectedPath(
                    paths,
                    MappingPlanPhase.Members,
                    terminal.AffectedPath.BranchOrigin)
            };
            return true;
        }

        if (!existingDestination &&
            plan.Observation.Rules.FirstOrDefault(rule =>
                rule.InvalidReason == MemberRuleInvalidReason.None &&
                rule.Origin is not
                    (MemberRuleOrigin.Convention or
                     MemberRuleOrigin.Ignore) &&
                rule.Lifecycle.HasFlag(
                    MemberLifecycleDependency.Creation) &&
                rule.Lifecycle.HasFlag(
                    MemberLifecycleDependency.Result) &&
                (rule.Lifecycle.HasFlag(
                     MemberLifecycleDependency.InitOnly) ||
                 rule.IsRequired &&
                 mapping.CreateFailure?.Reason ==
                     MappingFailureReason.ConstructorSelectionFailed)) is
                { } resultDependentRule)
        {
            failure = BuildFailure(
                mapping,
                resultDependentRule.SourceMapper,
                MappingFailureReason.MemberLifecycleInvalid,
                MemberLifecycleMessage,
                resultDependentRule.OriginNode ??
                resultDependentRule.DesignatorNode ??
                mapping.AnalysisContext.Registration.Syntax,
                paths);
            return true;
        }

        if (runtimeResult &&
            resultPolicy is
                ResultPolicyKind.ConstructUsing or
                ResultPolicyKind.ResolveUsing &&
            plan.Observation.Rules.FirstOrDefault(rule =>
                rule.InvalidReason == MemberRuleInvalidReason.None &&
                rule.Origin is not
                    (MemberRuleOrigin.Convention or
                     MemberRuleOrigin.Ignore) &&
                rule.Lifecycle.HasFlag(
                    MemberLifecycleDependency.InitOnly)) is
                { } lifecycleRule)
        {
            failure = BuildFailure(
                mapping,
                lifecycleRule.SourceMapper,
                MappingFailureReason.MemberLifecycleInvalid,
                MemberLifecycleMessage,
                lifecycleRule.OriginNode ??
                lifecycleRule.DesignatorNode ??
                mapping.AnalysisContext.Registration.Syntax,
                paths);
            return true;
        }

        failure = null!;
        return false;
    }

    private static bool CanApplyFailure(
        MappingFailureObservation? current,
        MappingFailureObservation replacement)
    {
        return current is null ||
               replacement.Reason ==
                   MappingFailureReason.MemberLifecycleInvalid &&
               current.Reason ==
                   MappingFailureReason.ConstructorSelectionFailed;
    }

    private static bool AppliesTo(
        MemberRuleObservation rule,
        bool existingDestination)
    {
        return existingDestination
            ? rule.Lifecycle.HasFlag(
                MemberLifecycleDependency.ExistingDestination)
            : rule.Lifecycle.HasFlag(
                MemberLifecycleDependency.Creation);
    }

    private static MappingFailureObservation BuildFailure(
        TypeMapperMappingModel mapping,
        INamedTypeSymbol? sourceMapper,
        MappingFailureReason reason,
        string message,
        SyntaxNode origin,
        MappingExecutionPathSet paths)
    {
        return MappingFailureObservation.Create(
            mapping.AnalysisContext,
            reason,
            message,
            MappingObservationOriginKind.Member,
            new MappingAffectedPath(
                paths,
                MappingPlanPhase.Members,
                origin),
            origin,
            sourceMapper);
    }

    internal static TypeMapperMappingModel ApplyPostResultFailure(
        TypeMapperMappingModel mapping,
        MappingFailureObservation failure,
        INamedTypeSymbol mapperType)
    {
        if (mapping.CreateFactory is not { } factory)
        {
            return mapping;
        }

        var guardedFactory = UserResultMappingPlanner.BuildFactoryMapping(
            mapping,
            ImmutableArray.Create<TypeMapperMemberMappingModel>(
                default(TypeMapperMemberMappingModel)),
            mapperType,
            factory.ValueExpression);
        var postFailure = new TypeMapperMemberControlFlowNode(
            Locals: ImmutableArray<TypeMapperLocalValueModel>.Empty,
            Condition: null,
            WhenTrue: null,
            WhenFalse: null,
            MemberMappings: ImmutableArray<TypeMapperMemberMappingModel>.Empty,
            ThrowExpression: null,
            Failure: failure,
            MemberObservation: mapping.MemberObservation);

        return mapping with
        {
            CreateFactory = guardedFactory,
            CreatePostMemberMappings = ImmutableArray<TypeMapperMemberMappingModel>.Empty,
            UpdateMemberMappings = ImmutableArray<TypeMapperMemberMappingModel>.Empty,
            PostMemberControlFlow = postFailure
        };
    }

    private static bool HasIntrinsicFailure(
        ConventionMemberMappingPlan plan)
    {
        return plan.Failure is not null ||
               plan.Observation.Rules.Any(static rule =>
                   rule.InvalidReason != MemberRuleInvalidReason.None ||
                   rule.InvalidReason == MemberRuleInvalidReason.None &&
                   rule.Origin is not
                       (MemberRuleOrigin.Convention or
                        MemberRuleOrigin.Ignore) &&
                   rule.Lifecycle.HasFlag(
                       MemberLifecycleDependency.Creation) &&
                   rule.Lifecycle.HasFlag(
                       MemberLifecycleDependency.Result) &&
                   (rule.IsRequired ||
                    rule.Lifecycle.HasFlag(
                        MemberLifecycleDependency.InitOnly))) ||
               plan.Observation.Terminals.Any(static terminal =>
                   terminal.Kind == StructuredTerminalKind.NullMembers);
    }

    private static bool HasRuntimeLifecycleFailure(
        ConventionMemberMappingPlan plan,
        ResultPolicyKind? resultPolicy)
    {
        return resultPolicy is
                   ResultPolicyKind.ConstructUsing or
                   ResultPolicyKind.ResolveUsing &&
               plan.Observation.Rules.Any(static rule =>
                   rule.InvalidReason == MemberRuleInvalidReason.None &&
                   rule.Origin is not
                       (MemberRuleOrigin.Convention or
                        MemberRuleOrigin.Ignore) &&
                   rule.Lifecycle.HasFlag(
                       MemberLifecycleDependency.InitOnly));
    }
}
