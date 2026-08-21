using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MappingCompletenessObservationBuilder
{
    public static TypeMapperMappingModel Attach(
        TypeMapperMappingModel mapping,
        PairConfigurationModel configuration,
        EffectiveMappingSettings effectiveSettings,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (!configuration.Manual.Conversions.IsEmpty)
        {
            return mapping with
            {
                CompletenessObservation = null
            };
        }

        var mappingSlices = EnumerateMappingSlices(mapping)
            .ToImmutableArray();
        var memberObservations = mappingSlices
            .Select(static slice => slice.MemberObservation)
            .OfType<MemberPlanningObservation>()
            .ToImmutableArray();
        var memberRules = memberObservations
            .SelectMany(static observation => observation.Rules)
            .ToImmutableArray();
        var declarativeSourceType =
            MappingTypeNormalization.NormalizeDeclarativeSource(
                configuration.Pair.SourceType,
                compilation);
        var supportedSourceMembers =
            DestinationCapabilityPolicy.IsOpaque(
                declarativeSourceType,
                compilation)
                ? ImmutableArray<ISymbol>.Empty
                : mapping.SourceMembers.IsDefault
                    ? ConventionMemberMappingPlanner.BuildReadableMembers(
                            declarativeSourceType,
                            compilation,
                            mapperType,
                            cancellationToken)
                        .Select(static member => member.Symbol)
                        .ToImmutableArray()
                    : mapping.SourceMembers
                        .Select(static member => member.Symbol)
                        .ToImmutableArray();
        var supportedDestinationMembers =
            ConventionMemberMappingPlanner.BuildWritableMembers(
                    configuration.Pair.DestinationType,
                    configuration.Pair.Capabilities,
                    compilation,
                    cancellationToken)
                .Select(static member => member.Symbol)
                .ToImmutableArray();

        void AddSupportedDestinationMember(ISymbol member)
        {
            if (!supportedDestinationMembers.Any(candidate =>
                    SymbolEqualityComparer.Default.Equals(
                        candidate,
                        member)))
            {
                supportedDestinationMembers =
                    supportedDestinationMembers.Add(member);
            }
        }
        var sourceUses =
            ImmutableArray.CreateBuilder<SourceUseObservation>();
        var sourceDiscards =
            ImmutableArray.CreateBuilder<SourceDiscardObservation>();
        var occupancy =
            ImmutableArray.CreateBuilder<
                DestinationOccupancyObservation>();
        var reachablePaths = GetReachablePaths(
            mapping,
            effectiveSettings);

        foreach (var pathMember in mapping.IncludedSourcePathMembers.IsDefault
                     ? ImmutableArray<ISymbol>.Empty
                     : mapping.IncludedSourcePathMembers)
        {
            AddSourceUse(
                sourceUses,
                pathMember,
                SourceUseKind.Semantic,
                mapping.AnalysisContext.Registration.Syntax);
        }

        var reachableRuleOrigins = memberRules
            .Where(rule =>
                rule.InvalidReason == MemberRuleInvalidReason.None &&
                IsRuleReachable(rule, reachablePaths))
            .Select(static rule => rule.OriginNode)
            .OfType<SyntaxNode>()
            .ToImmutableArray();
        var unreachableRuleOrigins = new HashSet<SyntaxNode>();

        foreach (var rule in memberRules)
        {
            if (rule.InvalidReason != MemberRuleInvalidReason.None ||
                IsRuleReachable(rule, reachablePaths))
            {
                continue;
            }

            if (rule.OriginNode is { } origin &&
                !reachableRuleOrigins.Any(candidate =>
                    IsSameSyntax(candidate, origin)))
            {
                unreachableRuleOrigins.Add(origin);
            }
        }

        foreach (var rule in memberRules)
        {
            if (rule.InvalidReason != MemberRuleInvalidReason.None ||
                !IsRuleReachable(rule, reachablePaths))
            {
                continue;
            }

            AddOccupancy(
                occupancy,
                rule.DestinationMember,
                rule.Origin,
                rule.OriginNode);

            if (rule.SourceMember is { } sourceMember)
            {
                foreach (var usedMember in
                         SourcePathOrMember(
                             rule.SourcePathMembers,
                             sourceMember))
                {
                    AddSourceUse(
                        sourceUses,
                        usedMember,
                        SourceUseKind.Semantic,
                        rule.OriginNode ??
                        mapping.AnalysisContext.Registration.Syntax);
                }
            }
        }

        foreach (var slice in mappingSlices)
        {
            if ((reachablePaths & MappingExecutionPathSet.NoPrevious) ==
                    MappingExecutionPathSet.None ||
                slice.CreateFailure is not null ||
                slice.ConstructorObservation?.SelectedConstructor is null)
            {
                continue;
            }

            var selectedCandidate = slice.ConstructorObservation.Candidates
                .FirstOrDefault(candidate =>
                    SymbolEqualityComparer.Default.Equals(
                        candidate.Constructor,
                        slice.ConstructorObservation
                            .SelectedConstructor));

            if (selectedCandidate is not null)
            {
                foreach (var rule in selectedCandidate.ParameterRules)
                {
                    if (rule.DestinationMember is { } associatedMember)
                    {
                        AddSupportedDestinationMember(associatedMember);
                    }

                    if (rule is
                        {
                            IsApplicable: true,
                            Origin: not ConstructorParameterRuleOrigin.Omitted
                                and not ConstructorParameterRuleOrigin.Ignore,
                            DestinationMember: { } destinationMember
                        })
                    {
                        AddOccupancy(
                            occupancy,
                            destinationMember,
                            MemberRuleOrigin.ConstructorArgument,
                            rule.OriginNode);

                        if (rule.SourceMember is { } sourceMember)
                        {
                            foreach (var usedMember in
                                     SourcePathOrMember(
                                         rule.SourcePathMembers,
                                         sourceMember))
                            {
                                AddSourceUse(
                                    sourceUses,
                                    usedMember,
                                    SourceUseKind.Semantic,
                                    rule.OriginNode ??
                                    slice.AnalysisContext.Registration.Syntax);
                            }
                        }
                    }
                }
            }
        }

        var callbacks = configuration.Declarative.ResultPolicies
            .Where(policy =>
                policy.Kind is
                    ResultPolicyKind.Resolve or
                    ResultPolicyKind.ResolveUsing ||
                (reachablePaths & MappingExecutionPathSet.NoPrevious) !=
                    MappingExecutionPathSet.None)
            .Select(policy =>
                (
                    policy.Expression,
                    AllowsCompileTimeDiscard: policy.Kind is
                        ResultPolicyKind.Construct or
                        ResultPolicyKind.Resolve
                ))
            .Concat(configuration.Declarative.Members.Select(members =>
                (
                    members.Expression,
                    AllowsCompileTimeDiscard: true
                )))
            .ToImmutableArray();

        foreach (var callback in callbacks)
        {
            ObserveCallback(
                callback.Expression,
                callback.AllowsCompileTimeDiscard,
                supportedSourceMembers,
                mapping.SourceMembers.IsDefault
                    ? ImmutableArray<ConventionReadableMember>.Empty
                    : mapping.SourceMembers,
                sourceUses,
                sourceDiscards,
                unreachableRuleOrigins,
                cancellationToken);
        }

        var errorDerivedUncertainty =
            ImmutableArray.CreateBuilder<ISymbol>();

        foreach (var slice in mappingSlices)
        {
            AddFailureUncertainty(
                slice.Failure,
                slice.ConstructorObservation);
            AddFailureUncertainty(
                slice.CreateFailure,
                slice.ConstructorObservation);
            AddFailureUncertainty(
                slice.UpdateFailure,
                slice.ConstructorObservation);
            AddFlatteningUncertainty(
                slice.MemberObservation?.FlatteningIssues ?? default);
            AddFlatteningUncertainty(
                slice.ConstructorObservation?.FlatteningIssues ?? default);
        }

        void AddFlatteningUncertainty(
            ImmutableArray<FlatteningIssueObservation> issues)
        {
            if (issues.IsDefaultOrEmpty)
            {
                return;
            }

            foreach (var issue in issues)
            {
                AddUncertain(issue.TargetSymbol);

                foreach (var candidateMember in issue.CandidateMembers)
                {
                    AddUncertain(candidateMember);
                }
            }
        }

        void AddFailureUncertainty(
            MappingFailureObservation? failure,
            ConstructorPlanningObservation? constructorObservation)
        {
            if (failure is null)
            {
                return;
            }

            if ((failure.AffectedPath.Paths & reachablePaths) ==
                MappingExecutionPathSet.None)
            {
                return;
            }

            var supportedMembers = supportedSourceMembers
                .AddRange(supportedDestinationMembers);

            if (failure.OffendingSymbol is { } offendingSymbol &&
                supportedMembers.Any(member =>
                    SymbolEqualityComparer.Default.Equals(
                        member,
                        offendingSymbol)))
            {
                AddUncertain(offendingSymbol);
            }

            foreach (var nested in failure.NestedObservations)
            {
                if (nested.TargetSymbol is { } targetSymbol)
                {
                    AddUncertain(targetSymbol);
                }

                if (nested.CurrentDestinationSymbol is
                    { } currentDestinationSymbol)
                {
                    AddUncertain(currentDestinationSymbol);
                }
            }

            if (failure.AffectedPath.Phase == MappingPlanPhase.Members)
            {
                foreach (var rule in memberObservations
                             .SelectMany(static observation =>
                                 observation.Rules)
                             .Where(rule => IsFailureRule(
                                 failure,
                                 rule)))
                {
                    AddUncertain(rule.DestinationMember);

                    if (rule.SourceMember is { } sourceMember)
                    {
                        foreach (var usedMember in
                                 SourcePathOrMember(
                                     rule.SourcePathMembers,
                                     sourceMember))
                        {
                            AddUncertain(usedMember);
                        }
                    }
                }
            }

            if (failure.AffectedPath.Phase is
                MappingPlanPhase.Configuration or
                MappingPlanPhase.Transfer or
                MappingPlanPhase.ResultSelection ||
                failure.OriginKind is
                    MappingObservationOriginKind.Callback or
                    MappingObservationOriginKind.CompilerPreflight)
            {
                foreach (var member in supportedMembers)
                {
                    AddUncertain(member);
                }
            }

            if (failure.AffectedPath.Phase ==
                MappingPlanPhase.Construction &&
                constructorObservation is { } constructor)
            {
                var candidates = constructor.SelectedConstructor is null
                    ? constructor.Candidates
                    : constructor.Candidates.Where(candidate =>
                            SymbolEqualityComparer.Default.Equals(
                                candidate.Constructor,
                                constructor.SelectedConstructor))
                        .ToImmutableArray();

                foreach (var rule in candidates.SelectMany(candidate =>
                             candidate.ParameterRules))
                {
                    if (rule.SourceMember is { } sourceMember)
                    {
                        foreach (var usedMember in
                                 SourcePathOrMember(
                                     rule.SourcePathMembers,
                                     sourceMember))
                        {
                            AddUncertain(usedMember);
                        }
                    }

                    if (rule.DestinationMember is { } destinationMember)
                    {
                        AddUncertain(destinationMember);
                    }
                }

                if (failure.Reason ==
                        MappingFailureReason.ConstructorSelectionFailed &&
                    candidates.Any(static candidate =>
                        candidate.RejectionReason ==
                            ConstructorCandidateRejectionReason
                                .RequiredMember))
                {
                    foreach (var required in memberObservations
                                 .SelectMany(static observation =>
                                     observation.RequiredObligations))
                    {
                        AddUncertain(required);
                    }
                }
            }
        }

        void AddUncertain(ISymbol member)
        {
            if (!errorDerivedUncertainty.Any(candidate =>
                    SymbolEqualityComparer.Default.Equals(
                        candidate,
                        member)))
            {
                errorDerivedUncertainty.Add(member);
            }
        }

        return mapping with
        {
            CompletenessObservation = new CompletenessPlanningObservation(
                supportedSourceMembers,
                supportedDestinationMembers,
                sourceUses.ToImmutable(),
                sourceDiscards.ToImmutable(),
                occupancy.ToImmutable(),
                errorDerivedUncertainty.ToImmutable())
        };
    }

    private static bool IsFailureRule(
        MappingFailureObservation failure,
        MemberRuleObservation rule)
    {
        return IsSameSyntax(failure.OriginNode, rule.OriginNode) ||
               IsSameSyntax(
                   failure.AffectedPath.BranchOrigin,
                   rule.OriginNode) ||
               IsSameSyntax(failure.OriginNode, rule.DesignatorNode) ||
               IsSameSyntax(
                   failure.AffectedPath.BranchOrigin,
                   rule.DesignatorNode) ||
               IsSameSyntax(
                   failure.OriginNode,
                   rule.ResultDependencyOrigin);
    }

    private static bool IsRuleReachable(
        MemberRuleObservation rule,
        MappingExecutionPathSet reachablePaths)
    {
        if (rule.Lifecycle == MemberLifecycleDependency.None)
        {
            return reachablePaths != MappingExecutionPathSet.None;
        }

        var creationReachable =
            (reachablePaths & MappingExecutionPathSet.NoPrevious) !=
                MappingExecutionPathSet.None &&
            rule.Lifecycle.HasFlag(
                MemberLifecycleDependency.Creation);
        var existingUpdateReachable = reachablePaths.HasFlag(
                MappingExecutionPathSet.UpdateWithPrevious) &&
            rule.Lifecycle.HasFlag(
                MemberLifecycleDependency.ExistingDestination);

        return creationReachable || existingUpdateReachable;
    }

    private static bool IsSameSyntax(SyntaxNode? left, SyntaxNode? right)
    {
        return left is not null &&
               right is not null &&
               ReferenceEquals(left.SyntaxTree, right.SyntaxTree) &&
               left.Span == right.Span;
    }

    private static MappingExecutionPathSet GetReachablePaths(
        TypeMapperMappingModel mapping,
        EffectiveMappingSettings settings)
    {
        if (!PolymorphicBasePlanReachability.IsReachable(mapping))
        {
            return MappingExecutionPathSet.None;
        }

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

    private static IEnumerable<TypeMapperMappingModel>
        EnumerateMappingSlices(TypeMapperMappingModel mapping)
    {
        yield return mapping;

        if (mapping.ControlFlow is not { } controlFlow)
        {
            yield break;
        }

        foreach (var slice in EnumerateMappingSlices(
                     controlFlow.CreateRoot))
        {
            yield return slice;
        }

        foreach (var slice in EnumerateMappingSlices(
                     controlFlow.UpdateRoot))
        {
            yield return slice;
        }
    }

    private static IEnumerable<TypeMapperMappingModel>
        EnumerateMappingSlices(TypeMapperControlFlowNode node)
    {
        if (node.Leaf is { } leaf)
        {
            foreach (var slice in EnumerateMappingSlices(leaf))
            {
                yield return slice;
            }
        }

        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            foreach (var slice in EnumerateMappingSlices(
                         evaluationContinuation))
            {
                yield return slice;
            }
        }

        foreach (var section in node.SwitchSections.IsDefault
                     ? ImmutableArray<TypeMapperSwitchSectionModel>.Empty
                     : node.SwitchSections)
        {
            foreach (var slice in EnumerateMappingSlices(section.Branch))
            {
                yield return slice;
            }
        }

        if (node.SwitchContinuation is { } switchContinuation)
        {
            foreach (var slice in EnumerateMappingSlices(
                         switchContinuation))
            {
                yield return slice;
            }
        }

        if (node.WhenTrue is { } whenTrue)
        {
            foreach (var slice in EnumerateMappingSlices(whenTrue))
            {
                yield return slice;
            }
        }

        if (node.WhenFalse is { } whenFalse)
        {
            foreach (var slice in EnumerateMappingSlices(whenFalse))
            {
                yield return slice;
            }
        }
    }

    private static void AddOccupancy(
        ImmutableArray<DestinationOccupancyObservation>.Builder observations,
        ISymbol member,
        MemberRuleOrigin origin,
        SyntaxNode? originNode)
    {
        if (observations.Any(observation =>
                observation.Origin == origin &&
                SymbolEqualityComparer.Default.Equals(
                    observation.Member,
                    member) &&
                ReferenceEquals(observation.OriginNode, originNode)))
        {
            return;
        }

        observations.Add(
            new DestinationOccupancyObservation(
                member,
                origin,
                originNode));
    }

    private static void ObserveCallback(
        BoundConfigurationExpression callback,
        bool allowsCompileTimeDiscard,
        ImmutableArray<ISymbol> supportedSourceMembers,
        ImmutableArray<ConventionReadableMember> sourceMembers,
        ImmutableArray<SourceUseObservation>.Builder sourceUses,
        ImmutableArray<SourceDiscardObservation>.Builder sourceDiscards,
        HashSet<SyntaxNode> unreachableRuleOrigins,
        CancellationToken cancellationToken)
    {
        if (callback.Syntax is not LambdaExpressionSyntax lambda)
        {
            foreach (var member in supportedSourceMembers)
            {
                AddSourceUse(
                    sourceUses,
                    member,
                    SourceUseKind.Potential,
                    callback.Syntax);
            }

            return;
        }

        var sourceParameterSyntax = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter,
            ParenthesizedLambdaExpressionSyntax parenthesized
                when parenthesized.ParameterList.Parameters.Count > 0 =>
                parenthesized.ParameterList.Parameters[0],
            _ => null
        };

        if (sourceParameterSyntax is null ||
            callback.SemanticModel.GetDeclaredSymbol(
                    sourceParameterSyntax,
                    cancellationToken) is not IParameterSymbol
                sourceParameter)
        {
            return;
        }

        var discardedStatements = new HashSet<SyntaxNode>();

        if (allowsCompileTimeDiscard && lambda.Block is not null)
        {
            foreach (var statement in lambda.Block.Statements
                         .OfType<ExpressionStatementSyntax>())
            {
                if (!DeclarativeControlFlowPlanner
                        .TryBuildCompileTimeSourceDiscard(
                            statement,
                            callback.SemanticModel,
                            cancellationToken,
                            out var discard))
                {
                    continue;
                }

                discardedStatements.Add(statement);
                AddSourceDiscard(discard.Member);

                foreach (var sourceMember in sourceMembers)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (sourceMember.SourceAccess is not { } access ||
                        !access.Path.Any(segment =>
                            AreSameMember(
                                segment.Symbol,
                                discard.Member)))
                    {
                        continue;
                    }

                    AddSourceDiscard(sourceMember.Symbol);
                }

                void AddSourceDiscard(ISymbol member)
                {
                    if (sourceDiscards.Any(candidate =>
                            ReferenceEquals(
                                candidate.Statement,
                                discard.Statement) &&
                            AreSameMember(
                                candidate.Member,
                                member)))
                    {
                        return;
                    }

                    sourceDiscards.Add(
                        new SourceDiscardObservation(
                            member,
                            discard.Statement,
                            callback));
                }
            }
        }

        foreach (var identifier in lambda.DescendantNodes(
                     descendIntoChildren: node =>
                         node is not LocalFunctionStatementSyntax)
                     .OfType<IdentifierNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SymbolEqualityComparer.Default.Equals(
                    callback.SemanticModel.GetSymbolInfo(
                            identifier,
                            cancellationToken)
                        .Symbol,
                    sourceParameter) ||
                identifier.Ancestors().Any(discardedStatements.Contains))
            {
                continue;
            }

            if (identifier.AncestorsAndSelf().Any(node =>
                    unreachableRuleOrigins.Any(origin =>
                        IsSameSyntax(node, origin))))
            {
                continue;
            }

            if (identifier.Ancestors().Any(ancestor =>
                    callback.SemanticModel.GetOperation(
                        ancestor,
                        cancellationToken) is INameOfOperation))
            {
                continue;
            }

            if (identifier.Parent is MemberAccessExpressionSyntax
                {
                    Expression: var receiver
                } memberAccess &&
                ReferenceEquals(receiver, identifier))
            {
                var sourceMember = callback.SemanticModel.GetSymbolInfo(
                        memberAccess,
                        cancellationToken)
                    .Symbol;

                if (sourceMember is IPropertySymbol or IFieldSymbol)
                {
                    AddSourceUse(
                        sourceUses,
                        sourceMember,
                        SourceUseKind.Semantic,
                        memberAccess);
                    continue;
                }
            }

            else if (identifier.Parent is IsPatternExpressionSyntax
                     {
                         Expression: var typeTestExpression
                     } isPattern &&
                     ReferenceEquals(typeTestExpression, identifier))
            {
                ObservePatternSourceUses(
                    isPattern,
                    callback,
                    supportedSourceMembers,
                    sourceUses,
                    cancellationToken);
                continue;
            }

            else if (identifier.Parent is BinaryExpressionSyntax
                      {
                          RawKind: (int)SyntaxKind.IsExpression,
                          Left: var legacyTypeTestExpression
                      } &&
                      ReferenceEquals(
                          legacyTypeTestExpression,
                          identifier))
            {
                continue;
            }

            foreach (var member in supportedSourceMembers)
            {
                AddSourceUse(
                    sourceUses,
                    member,
                    SourceUseKind.Potential,
                    identifier);
            }
        }
    }

    private static bool AreSameMember(ISymbol left, ISymbol right)
    {
        if (SymbolEqualityComparer.Default.Equals(left, right))
        {
            return true;
        }

        var leftContainingType = left.ContainingType is { } leftType
            ? MapperContractDisplay.CreateType(leftType)
            : string.Empty;
        var rightContainingType = right.ContainingType is { } rightType
            ? MapperContractDisplay.CreateType(rightType)
            : string.Empty;

        return StringComparer.Ordinal.Equals(
                   leftContainingType,
                   rightContainingType) &&
               StringComparer.Ordinal.Equals(left.Name, right.Name);
    }

    private static void ObservePatternSourceUses(
        IsPatternExpressionSyntax isPattern,
        BoundConfigurationExpression callback,
        ImmutableArray<ISymbol> supportedSourceMembers,
        ImmutableArray<SourceUseObservation>.Builder sourceUses,
        CancellationToken cancellationToken)
    {
        if (isPattern.Pattern.DescendantNodesAndSelf()
            .OfType<RecursivePatternSyntax>()
            .Any(static pattern =>
                pattern.PositionalPatternClause is not null))
        {
            foreach (var member in supportedSourceMembers)
            {
                AddSourceUse(
                    sourceUses,
                    member,
                    SourceUseKind.Potential,
                    isPattern);
            }

            return;
        }

        var operation = callback.SemanticModel.GetOperation(
            isPattern,
            cancellationToken);

        if (operation is null)
        {
            return;
        }

        foreach (var reference in operation.DescendantsAndSelf()
                     .OfType<IMemberReferenceOperation>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reference.Member is IPropertySymbol or IFieldSymbol)
            {
                AddSourceUse(
                    sourceUses,
                    reference.Member,
                    SourceUseKind.Semantic,
                    isPattern);
            }
        }
    }

    private static void AddSourceUse(
        ImmutableArray<SourceUseObservation>.Builder observations,
        ISymbol member,
        SourceUseKind kind,
        SyntaxNode origin)
    {
        if (observations.Any(observation =>
                observation.Kind == kind &&
                SymbolEqualityComparer.Default.Equals(
                    observation.Member,
                    member) &&
                ReferenceEquals(observation.OriginNode, origin)))
        {
            return;
        }

        observations.Add(new SourceUseObservation(member, kind, origin));
    }

    private static IEnumerable<ISymbol> SourcePathOrMember(
        ImmutableArray<ISymbol> path,
        ISymbol member) =>
        path.IsDefaultOrEmpty
            ? new[] { member }
            : path;
}
