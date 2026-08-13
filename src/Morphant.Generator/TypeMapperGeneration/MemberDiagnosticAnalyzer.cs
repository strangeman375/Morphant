using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MemberDiagnosticAnalyzer
{
    private const string AutoUnavailableReason =
        "Auto could not find exactly one compatible source member";

    private const string RuntimeResultReason =
        "init-only member cannot be assigned after ConstructUsing or " +
        "ResolveUsing returns";

    private const string ResultDependencyReason =
        "member rule uses 'result' before the destination is created";

    public static ImmutableArray<MemberDiagnosticCandidate> Build(
        MapperContractAnalysis analysis,
        TypeMapperModel model,
        ImmutableArray<CallbackDiagnosticCandidate> callbackDiagnostics,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<MemberDiagnosticCandidate>();

        foreach (var pair in analysis.Configuration.Pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (analysis.Excludes(pair.Pair.Identity) ||
                pair.Conflicts != PairConfigurationConflict.None ||
                !TryGetMapping(model, pair.Pair.Identity, out var mapping) ||
                !CanAnalyze(mapping))
            {
                continue;
            }

            var paths = GetReachablePaths(mapping);

            if (paths == MappingExecutionPathSet.None)
            {
                continue;
            }

            var context = new MemberAnalysisContext(
                analysis.Configuration,
                pair,
                mapping);

            AnalyzeMapping(
                context,
                mapping,
                paths,
                result,
                cancellationToken);
        }

        return result
            .Where(candidate => !callbackDiagnostics.Any(callback =>
                Suppresses(candidate, callback)))
            .ToImmutableArray();
    }

    private static bool Suppresses(
        MemberDiagnosticCandidate member,
        CallbackDiagnosticCandidate callback)
    {
        if (!StringComparer.Ordinal.Equals(member.PairKey, callback.PairKey))
        {
            return false;
        }

        var callbackLocation = callback.Diagnostic.Location;
        var scope = member.ScopeLocation;

        return StringComparer.Ordinal.Equals(
                   LocationPath(scope),
                   LocationPath(callbackLocation)) &&
               scope.SourceSpan.Contains(callbackLocation.SourceSpan);
    }

    private static void AnalyzeMapping(
        MemberAnalysisContext context,
        TypeMapperMappingModel mapping,
        MappingExecutionPathSet paths,
        ImmutableArray<MemberDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (mapping.Failure is { } mappingFailure &&
            IsPriorFailure(mapping, mappingFailure))
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
                    createRoot: true,
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
                    createRoot: false,
                    result,
                    cancellationToken);
            }

            return;
        }

        AnalyzeLeaf(
            context,
            mapping,
            paths & MappingExecutionPathSet.NoPrevious,
            existingDestination: false,
            runtimeResult: false,
            result,
            cancellationToken);
        AnalyzeLeaf(
            context,
            mapping,
            paths & MappingExecutionPathSet.UpdateWithPrevious,
            existingDestination: true,
            runtimeResult: false,
            result,
            cancellationToken);
    }

    private static void AnalyzeNode(
        MemberAnalysisContext context,
        TypeMapperControlFlowNode node,
        MappingExecutionPathSet paths,
        bool createRoot,
        ImmutableArray<MemberDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (node.Leaf is { } leaf)
        {
            var replacement = !createRoot &&
                (leaf.CreateFactory is not null ||
                 leaf.CreateConstructor is not null);
            var runtimeResult = leaf.CreateFactory is not null &&
                FindRuntimeResultPolicy(context.Pair, leaf) is not null;

            AnalyzeLeaf(
                context,
                leaf,
                paths,
                existingDestination: !createRoot && !replacement,
                runtimeResult,
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
                createRoot,
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
                    createRoot,
                    result,
                    cancellationToken);
            }

            if (node.SwitchContinuation is { } continuation)
            {
                AnalyzeNode(
                    context,
                    continuation,
                    paths,
                    createRoot,
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
            createRoot,
            result,
            cancellationToken);
        AnalyzeNode(
            context,
            node.WhenFalse!,
            paths,
            createRoot,
            result,
            cancellationToken);
    }

    private static void AnalyzeLeaf(
        MemberAnalysisContext context,
        TypeMapperMappingModel mapping,
        MappingExecutionPathSet paths,
        bool existingDestination,
        bool runtimeResult,
        ImmutableArray<MemberDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        if (paths == MappingExecutionPathSet.None ||
            mapping.MemberObservation is not { } observation)
        {
            return;
        }

        var pathFailure = existingDestination
            ? mapping.UpdateFailure
            : mapping.CreateFailure;

        if (pathFailure is { } failure &&
            IsPriorFailure(mapping, failure))
        {
            return;
        }

        if (mapping.PostMemberControlFlow is { } postMemberControlFlow)
        {
            AddConstructionMemberCandidates(
                context,
                mapping,
                observation,
                pathFailure,
                paths,
                result,
                cancellationToken);
            AnalyzeMemberNode(
                context,
                mapping,
                postMemberControlFlow,
                paths,
                existingDestination,
                runtimeResult,
                result,
                cancellationToken);
            return;
        }

        AddInvalidRuleCandidates(
            context,
            mapping,
            observation,
            paths,
            existingDestination,
            result,
            cancellationToken);
        AddConstructionMemberCandidates(
            context,
            mapping,
            observation,
            pathFailure,
            paths,
            result,
            cancellationToken);
        AddRuntimeLifecycleCandidates(
            context,
            mapping,
            observation,
            paths,
            runtimeResult,
            result,
            cancellationToken);
        AddNullPlanCandidates(
            context,
            mapping,
            observation,
            paths,
            result);
    }

    private static void AnalyzeMemberNode(
        MemberAnalysisContext context,
        TypeMapperMappingModel mapping,
        TypeMapperMemberControlFlowNode node,
        MappingExecutionPathSet paths,
        bool existingDestination,
        bool runtimeResult,
        ImmutableArray<MemberDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (node.MemberObservation is { } observation)
        {
            AddInvalidRuleCandidates(
                context,
                mapping,
                observation,
                paths,
                existingDestination,
                result,
                cancellationToken);
            AddRuntimeLifecycleCandidates(
                context,
                mapping,
                observation,
                paths,
                runtimeResult,
                result,
                cancellationToken);
            AddNullPlanCandidates(
                context,
                mapping,
                observation,
                paths,
                result);
            return;
        }

        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            AnalyzeMemberNode(
                context,
                mapping,
                evaluationContinuation,
                paths,
                existingDestination,
                runtimeResult,
                result,
                cancellationToken);
            return;
        }

        if (node.SwitchExpression is not null)
        {
            foreach (var section in node.SwitchSections)
            {
                AnalyzeMemberNode(
                    context,
                    mapping,
                    section.Branch,
                    paths,
                    existingDestination,
                    runtimeResult,
                    result,
                    cancellationToken);
            }

            if (node.SwitchContinuation is { } continuation)
            {
                AnalyzeMemberNode(
                    context,
                    mapping,
                    continuation,
                    paths,
                    existingDestination,
                    runtimeResult,
                    result,
                    cancellationToken);
            }

            return;
        }

        if (node.Condition is null)
        {
            return;
        }

        AnalyzeMemberNode(
            context,
            mapping,
            node.WhenTrue!,
            paths,
            existingDestination,
            runtimeResult,
            result,
            cancellationToken);
        AnalyzeMemberNode(
            context,
            mapping,
            node.WhenFalse!,
            paths,
            existingDestination,
            runtimeResult,
            result,
            cancellationToken);
    }

    private static void AddInvalidRuleCandidates(
        MemberAnalysisContext context,
        TypeMapperMappingModel mapping,
        MemberPlanningObservation observation,
        MappingExecutionPathSet paths,
        bool existingDestination,
        ImmutableArray<MemberDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        foreach (var rule in observation.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (rule.InvalidReason == MemberRuleInvalidReason.None ||
                existingDestination &&
                !rule.Lifecycle.HasFlag(
                    MemberLifecycleDependency.ExistingDestination) ||
                !existingDestination &&
                !rule.Lifecycle.HasFlag(MemberLifecycleDependency.Creation))
            {
                continue;
            }

            var additional = ImmutableArray.CreateBuilder<Location>();
            Location primary;
            string reason;
            string detail;

            switch (rule.InvalidReason)
            {
                case MemberRuleInvalidReason.AutoUnavailable:
                    primary = GetMarkerLocation(rule.OriginNode, "Auto");
                    reason = AutoUnavailableReason;
                    detail = "auto";
                    break;

                case MemberRuleInvalidReason.MarkerTargetMismatch:
                    primary = GetMarkerLocation(
                        rule.OriginNode,
                        rule.Origin switch
                        {
                            MemberRuleOrigin.Auto => "Auto",
                            MemberRuleOrigin.Ignore => "Ignore",
                            _ => "Value"
                        });
                    reason = "specified type '" +
                        DisplayType(rule.AssertedType!) +
                        "' does not match member type '" +
                        DisplayType(
                            rule.TargetType ??
                            GetMemberType(rule.DestinationMember)) +
                        "'";
                    detail = "marker";
                    break;

                case MemberRuleInvalidReason.ImportedSlotHidden:
                    primary = GetIncludeBaseLocation(
                        context.Pair,
                        rule,
                        cancellationToken) ??
                        rule.OriginNode?.GetLocation() ??
                        mapping.AnalysisContext.Registration.Syntax
                            .GetLocation();
                    reason = "IncludeBase rule for destination member '" +
                        DisplayMember(rule.DestinationMember) +
                        "', which is hidden by '" +
                        DisplayMember(rule.HiddenImportedSlot!) +
                        "' in the current destination";
                    detail = "hidden";

                    if (rule.DesignatorNode is { } designator)
                    {
                        AddDistinct(
                            additional,
                            designator.GetLocation(),
                            primary);
                    }

                    if (GetDeclarationLocation(
                            rule.HiddenImportedSlot!,
                            cancellationToken) is { } hidingLocation)
                    {
                        AddDistinct(additional, hidingLocation, primary);
                    }

                    break;

                default:
                    continue;
            }

            result.Add(CreateCandidate(
                MemberDiagnosticKind.InvalidRule,
                context,
                mapping,
                rule.SourceMapper,
                rule.DestinationMember,
                primary,
                additional.ToImmutable(),
                detail,
                reason,
                paths,
                rule.OriginNode ?? observation.PlanOrigin));
        }
    }

    private static void AddConstructionMemberCandidates(
        MemberAnalysisContext context,
        TypeMapperMappingModel mapping,
        MemberPlanningObservation observation,
        MappingFailureObservation? failure,
        MappingExecutionPathSet paths,
        ImmutableArray<MemberDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        if (failure is null)
        {
            return;
        }

        if (failure.Reason == MappingFailureReason.MemberLifecycleInvalid)
        {
            AddResultDependentLifecycleCandidates(
                context,
                mapping,
                observation,
                failure,
                paths,
                result,
                cancellationToken);
            return;
        }

        if (mapping.ConstructorObservation is not { } constructor ||
            failure.Reason is not
                (MappingFailureReason.ConstructorSelectionFailed or
                 MappingFailureReason.ConstructorParameterRuleInvalid))
        {
            return;
        }

        if (HasSelectedRejection(
                constructor,
                ConstructorCandidateRejectionReason
                    .ResultDependentInitializer))
        {
            AddResultDependentLifecycleCandidates(
                context,
                mapping,
                observation,
                failure,
                paths,
                result,
                cancellationToken);

            return;
        }

        if (!HasSelectedRejection(
                constructor,
                ConstructorCandidateRejectionReason.RequiredMember))
        {
            return;
        }

        foreach (var required in observation.RequiredObligations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rule = observation.Rules.FirstOrDefault(candidate =>
                SymbolEqualityComparer.Default.Equals(
                    candidate.DestinationMember,
                    required));
            var primary = rule is
                {
                    Origin: MemberRuleOrigin.Ignore,
                    OriginNode: { } ignore
                }
                    ? GetMarkerLocation(ignore, "Ignore")
                    : observation.PlanOrigin?.GetLocation() ??
                      GetDeclarationLocation(required, cancellationToken) ??
                      GetInvocationNameLocation(
                          mapping.AnalysisContext.Registration.Syntax);
            var additional = ImmutableArray.CreateBuilder<Location>();

            if (GetEffectiveMemberSettingOrigin(context) is
                { } settingOrigin)
            {
                AddDistinct(
                    additional,
                    settingOrigin.GetLocation(),
                    primary);
            }

            if (constructor.SelectedConstructor is { } selected &&
                GetDeclarationLocation(selected, cancellationToken) is
                    { } constructorLocation)
            {
                AddDistinct(additional, constructorLocation, primary);
            }

            result.Add(CreateCandidate(
                MemberDiagnosticKind.RequiredMember,
                context,
                mapping,
                rule?.SourceMapper,
                required,
                primary,
                additional.ToImmutable(),
                "required",
                reason: string.Empty,
                paths,
                rule?.OriginNode ?? observation.PlanOrigin));
        }
    }

    private static void AddResultDependentLifecycleCandidates(
        MemberAnalysisContext context,
        TypeMapperMappingModel mapping,
        MemberPlanningObservation observation,
        MappingFailureObservation failure,
        MappingExecutionPathSet paths,
        ImmutableArray<MemberDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        foreach (var rule in observation.Rules)
        {
            if (rule.InvalidReason != MemberRuleInvalidReason.None ||
                !rule.Lifecycle.HasFlag(
                    MemberLifecycleDependency.Result) ||
                !(rule.IsRequired ||
                  rule.Lifecycle.HasFlag(
                      MemberLifecycleDependency.InitOnly)))
            {
                continue;
            }

            var primary = rule.DesignatorNode?.GetLocation() ??
                rule.OriginNode?.GetLocation() ??
                failure.PrimaryLocation;
            var additional = ImmutableArray.CreateBuilder<Location>();

            if (rule.ResultDependencyOrigin is { } dependency)
            {
                AddDistinct(
                    additional,
                    dependency.GetLocation(),
                    primary);
            }

            AddImportLocation(
                additional,
                context.Pair,
                rule,
                primary,
                cancellationToken);
            result.Add(CreateCandidate(
                MemberDiagnosticKind.UnavailableLifecycle,
                context,
                mapping,
                rule.SourceMapper,
                rule.DestinationMember,
                primary,
                additional.ToImmutable(),
                "result-dependency",
                ResultDependencyReason,
                paths,
                rule.OriginNode ?? observation.PlanOrigin));
        }
    }

    private static void AddRuntimeLifecycleCandidates(
        MemberAnalysisContext context,
        TypeMapperMappingModel mapping,
        MemberPlanningObservation observation,
        MappingExecutionPathSet paths,
        bool runtimeResult,
        ImmutableArray<MemberDiagnosticCandidate>.Builder result,
        CancellationToken cancellationToken)
    {
        if (!runtimeResult ||
            FindRuntimeResultPolicy(context.Pair, mapping) is not
                { } resultPolicy)
        {
            return;
        }

        foreach (var rule in observation.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (rule.InvalidReason != MemberRuleInvalidReason.None ||
                rule.Origin is
                    MemberRuleOrigin.Convention or
                    MemberRuleOrigin.Ignore ||
                !rule.Lifecycle.HasFlag(
                    MemberLifecycleDependency.InitOnly))
            {
                continue;
            }

            var primary = rule.DesignatorNode?.GetLocation() ??
                rule.OriginNode?.GetLocation() ??
                resultPolicy.Invocation.GetLocation();
            var additional = ImmutableArray.CreateBuilder<Location>();
            AddDistinct(
                additional,
                GetInvocationNameLocation(resultPolicy.Invocation),
                primary);
            AddImportLocation(
                additional,
                context.Pair,
                rule,
                primary,
                cancellationToken);

            result.Add(CreateCandidate(
                MemberDiagnosticKind.UnavailableLifecycle,
                context,
                mapping,
                rule.SourceMapper,
                rule.DestinationMember,
                primary,
                additional.ToImmutable(),
                "runtime-result",
                RuntimeResultReason,
                paths,
                rule.OriginNode ?? observation.PlanOrigin));
        }
    }

    private static void AddNullPlanCandidates(
        MemberAnalysisContext context,
        TypeMapperMappingModel mapping,
        MemberPlanningObservation observation,
        MappingExecutionPathSet paths,
        ImmutableArray<MemberDiagnosticCandidate>.Builder result)
    {
        foreach (var terminal in observation.Terminals)
        {
            if (terminal.Kind != StructuredTerminalKind.NullMembers)
            {
                continue;
            }

            var primary = terminal.OriginNode.GetLocation();
            var additional = (terminal.Aliases.IsDefault
                    ? ImmutableArray<DeclarativeTerminalAliasSyntax>.Empty
                    : terminal.Aliases)
                .Select(static alias => alias.Use.GetLocation())
                .Where(location => !SameLocation(location, primary))
                .Distinct(LocationComparer.Instance)
                .OrderBy(LocationPath, StringComparer.Ordinal)
                .ThenBy(static location => location.SourceSpan.Start)
                .ToImmutableArray();

            result.Add(CreateCandidate(
                MemberDiagnosticKind.NullMembersPlan,
                context,
                mapping,
                sourceMapper: null,
                member: null,
                primary,
                additional,
                "null-plan|" + LocationIdentity(primary),
                reason: string.Empty,
                paths,
                terminal.OriginNode));
        }
    }

    private static MemberDiagnosticCandidate CreateCandidate(
        MemberDiagnosticKind kind,
        MemberAnalysisContext context,
        TypeMapperMappingModel mapping,
        INamedTypeSymbol? sourceMapper,
        ISymbol? member,
        Location primary,
        ImmutableArray<Location> additionalLocations,
        string detail,
        string reason,
        MappingExecutionPathSet paths,
        SyntaxNode? scopeOrigin)
    {
        var resolvedSourceMapper = (sourceMapper ??
            context.Pair.Origin.DeclaringMapperType).OriginalDefinition;
        var mapperIdentity = SymbolNameHelper.GetFullMetadataName(
            resolvedSourceMapper);
        var registration = context.Pair.Origin.DeclaredRegistration;
        var pairKey = MappingTypeIdentityPolicy.Create(
                registration.SourceType).Key +
            "->" + MappingTypeIdentityPolicy.Create(
                registration.DestinationType).Key;
        var originIdentity = LocationIdentity(primary);
        var memberIdentity = member is null
            ? string.Empty
            : member.GetDocumentationCommentId() ?? DisplayMember(member);
        var identity = ((int)kind).ToString(CultureInfo.InvariantCulture) +
            "|" + mapperIdentity +
            "|" + pairKey +
            "|" + memberIdentity +
            "|" + originIdentity +
            "|" + detail;
        var memberOrder = member is null
            ? int.MaxValue
            : GetMemberOrder(mapping.MemberObservation, member);

        return new MemberDiagnosticCandidate(
            kind,
            identity,
            mapperIdentity,
            context.Pair.Origin.LevelOrder,
            pairKey,
            originIdentity,
            memberOrder,
            primary.SourceSpan.Start,
            detail,
            SymbolEqualityComparer.Default.Equals(
                context.Pair.Origin.DeclaringMapperType.OriginalDefinition,
                resolvedSourceMapper),
            primary,
            (scopeOrigin ??
             mapping.AnalysisContext.Registration.Syntax).GetLocation(),
            additionalLocations,
            MapperContractDisplay.Create(
                registration.SourceType,
                registration.DestinationType),
            member?.Name ?? string.Empty,
            reason,
            paths);
    }

    private static bool HasSelectedRejection(
        ConstructorPlanningObservation observation,
        ConstructorCandidateRejectionReason reason)
    {
        if (observation.SelectedConstructor is { } selected)
        {
            var candidate = observation.Candidates.FirstOrDefault(item =>
                ConventionConstructorMappingPlanner.AreSameConstructor(
                    item.Constructor,
                    selected));

            return candidate?.RejectionReason == reason;
        }

        return observation.Candidates.Length == 1 &&
               observation.Candidates[0].RejectionReason == reason;
    }

    private static ResultPolicyConfigurationModel? FindRuntimeResultPolicy(
        PairConfigurationModel pair,
        TypeMapperMappingModel mapping)
    {
        if (mapping.CreateFactory is null &&
            mapping.ControlFlow is null)
        {
            return null;
        }

        return pair.Declarative.ResultPolicies.FirstOrDefault(policy =>
            policy.Kind is
                ResultPolicyKind.ConstructUsing or
                ResultPolicyKind.ResolveUsing);
    }

    private static Location? GetIncludeBaseLocation(
        PairConfigurationModel pair,
        MemberRuleObservation rule,
        CancellationToken cancellationToken)
    {
        foreach (var includeBase in pair.Composition.IncludeBaseCalls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (rule.DestinationMember.ContainingType is { } containingType &&
                AreSameTypeIdentity(
                    includeBase.DestinationType,
                    containingType))
            {
                return GetInvocationNameLocation(includeBase.Invocation);
            }
        }

        return pair.Composition.IncludeBaseCalls.IsEmpty
            ? null
            : GetInvocationNameLocation(
                pair.Composition.IncludeBaseCalls[0].Invocation);
    }

    private static void AddImportLocation(
        ImmutableArray<Location>.Builder locations,
        PairConfigurationModel pair,
        MemberRuleObservation rule,
        Location primary,
        CancellationToken cancellationToken)
    {
        if (rule.SourceMapper is not null &&
            !AreSameTypeIdentity(
                rule.SourceMapper,
                pair.Origin.DeclaringMapperType) &&
            GetIncludeBaseLocation(pair, rule, cancellationToken) is
                { } includeLocation)
        {
            AddDistinct(locations, includeLocation, primary);
        }
    }

    private static bool AreSameTypeIdentity(
        ITypeSymbol left,
        ITypeSymbol right) =>
        SymbolEqualityComparer.Default.Equals(left, right) ||
        StringComparer.Ordinal.Equals(
            MappingTypeIdentityPolicy.Create(left).Key,
            MappingTypeIdentityPolicy.Create(right).Key);

    private static SyntaxNode? GetEffectiveMemberSettingOrigin(
        MemberAnalysisContext context)
    {
        static SyntaxNode? Resolve(PairConfigurationSettings settings)
        {
            var value = settings.MemberSelection;

            if (value.Origin == PairConfigurationSettingOrigin.Unset ||
                value.Value is null)
            {
                return null;
            }

            return value.Syntax is InvocationExpressionSyntax invocation
                ? invocation.ArgumentList.Arguments.FirstOrDefault()
                    ?.Expression
                : value.Syntax;
        }

        if (Resolve(context.Pair.Settings) is { } local)
        {
            return local;
        }

        foreach (var settings in context.Pair.Composition.IncludedBaseSettings)
        {
            if (Resolve(settings) is { } included)
            {
                return included;
            }
        }

        if (Resolve(context.Mapper.RootSettings) is { } root)
        {
            return root;
        }

        foreach (var settings in context.Mapper.BaseRootSettings)
        {
            if (Resolve(settings) is { } baseRoot)
            {
                return baseRoot;
            }
        }

        return null;
    }

    private static int GetMemberOrder(
        MemberPlanningObservation? observation,
        ISymbol member)
    {
        if (observation is not { } value)
        {
            return int.MaxValue;
        }

        var members = value.SupportedDestinationMembers
            .AddRange(value.RequiredObligations);

        for (var index = 0; index < members.Length; index++)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    members[index],
                    member))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static ITypeSymbol GetMemberType(ISymbol member) => member switch
    {
        IPropertySymbol property => property.Type.WithNullableAnnotation(
            property.NullableAnnotation),
        IFieldSymbol field => field.Type.WithNullableAnnotation(
            field.NullableAnnotation),
        _ => throw new InvalidOperationException(
            $"Unsupported destination member symbol: {member.Kind}.")
    };

    private static string DisplayType(ITypeSymbol type) =>
        MapperContractDisplay.CreateType(type);

    private static string DisplayMember(ISymbol member)
    {
        var containing = member.ContainingType is { } containingType
            ? MapperContractDisplay.CreateType(containingType)
            : string.Empty;

        return containing + "." + member.Name;
    }

    private static Location GetMarkerLocation(
        SyntaxNode? origin,
        string markerName)
    {
        if (origin is not null)
        {
            foreach (var invocation in origin.DescendantNodesAndSelf()
                         .OfType<InvocationExpressionSyntax>())
            {
                var name = invocation.Expression switch
                {
                    MemberAccessExpressionSyntax access => access.Name,
                    SimpleNameSyntax simple => simple,
                    _ => null
                };

                if (name?.Identifier.ValueText == markerName)
                {
                    return name.Identifier.GetLocation();
                }
            }
        }

        return origin?.GetLocation() ?? Location.None;
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
                memberAccess.Name.Identifier.GetLocation(),
            GenericNameSyntax genericName =>
                genericName.Identifier.GetLocation(),
            SimpleNameSyntax simple => simple.Identifier.GetLocation(),
            _ => invocation.Expression.GetLocation()
        };
    }

    private static Location? GetDeclarationLocation(
        ISymbol symbol,
        CancellationToken cancellationToken)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            var syntax = reference.GetSyntax(cancellationToken);

            return syntax switch
            {
                PropertyDeclarationSyntax property =>
                    property.Identifier.GetLocation(),
                VariableDeclaratorSyntax variable =>
                    variable.Identifier.GetLocation(),
                FieldDeclarationSyntax field =>
                    field.Declaration.Variables.FirstOrDefault()
                        ?.Identifier.GetLocation(),
                ConstructorDeclarationSyntax constructor =>
                    constructor.Identifier.GetLocation(),
                _ => syntax.GetLocation()
            };
        }

        return null;
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
                    Settings.NullDestinationHandlingValue.Create)
            {
                result |= MappingExecutionPathSet.UpdateWithoutPrevious;
            }
        }

        return result;
    }

    private static bool CanAnalyze(TypeMapperMappingModel mapping)
    {
        return mapping.ManualMapping is null &&
               mapping.EffectiveSettings.HasExecutableOperation &&
               !(mapping.Failure is { } failure &&
                 IsPriorCategoryFailure(failure.Reason));
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
            MappingFailureReason.StructuredResultRequiresDestination or
            MappingFailureReason.NestedPairUnknown or
            MappingFailureReason.NestedResultIncompatible or
            MappingFailureReason.NestedUpdateDestinationInvalid;
    }

    private static bool IsPriorFailure(
        TypeMapperMappingModel mapping,
        MappingFailureObservation failure)
    {
        if (IsPriorCategoryFailure(failure.Reason))
        {
            return true;
        }

        if (failure.Reason is not
            (MappingFailureReason.MissingConstructionPolicy or
             MappingFailureReason.ConstructorSelectionFailed or
             MappingFailureReason.ConstructorParameterRuleInvalid or
             MappingFailureReason.TerminalPreviousWithoutValue or
             MappingFailureReason.TerminalNullConstruction))
        {
            return false;
        }

        if (failure.Reason !=
                MappingFailureReason.ConstructorSelectionFailed ||
            mapping.ConstructorObservation is not { } constructor)
        {
            return true;
        }

        return !HasSelectedRejection(
                   constructor,
                   ConstructorCandidateRejectionReason.RequiredMember) &&
               !HasSelectedRejection(
                   constructor,
                   ConstructorCandidateRejectionReason
                       .ResultDependentInitializer);
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

    private readonly record struct MemberAnalysisContext(
        MapperPairConfigurationModel Mapper,
        PairConfigurationModel Pair,
        TypeMapperMappingModel Mapping);

    private sealed class LocationComparer : IEqualityComparer<Location>
    {
        public static readonly LocationComparer Instance = new();

        public bool Equals(Location? x, Location? y) =>
            x is not null && y is not null && SameLocation(x, y);

        public int GetHashCode(Location obj)
        {
            unchecked
            {
                var hash = obj.SourceTree?.GetHashCode() ?? 0;
                hash = (hash * 397) ^ obj.SourceSpan.Start;
                return (hash * 397) ^ obj.SourceSpan.Length;
            }
        }
    }
}
