using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MappingCompletenessObservationBuilder
{
    public static TypeMapperMappingModel Attach(
        TypeMapperMappingModel mapping,
        PairConfigurationModel configuration,
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
        var supportedSourceMembers = memberObservations.IsEmpty
            ? ConventionMemberMappingPlanner.BuildReadableMembers(
                    MappingTypeNormalization.NormalizeDeclarativeSource(
                        configuration.Pair.SourceType,
                        compilation),
                    compilation,
                    mapperType,
                    cancellationToken)
                .Select(static member => member.Symbol)
                .ToImmutableArray()
            : DistinctSymbols(memberObservations.SelectMany(
                static observation => observation.SupportedSourceMembers));
        var supportedDestinationMembers = memberObservations.IsEmpty
            ? ConventionMemberMappingPlanner.BuildWritableMembers(
                    configuration.Pair.DestinationType,
                    configuration.Pair.Capabilities,
                    compilation,
                    cancellationToken)
                .Select(static member => member.Symbol)
                .ToImmutableArray()
            : DistinctSymbols(memberObservations.SelectMany(
                static observation =>
                    observation.SupportedDestinationMembers));

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

        foreach (var memberObservation in memberObservations)
        {
            foreach (var rule in memberObservation.Rules)
            {
                AddOccupancy(
                    occupancy,
                    rule.DestinationMember,
                    rule.Origin,
                    rule.OriginNode);

                if (rule.SourceMember is { } sourceMember)
                {
                    AddSourceUse(
                        sourceUses,
                        sourceMember,
                        SourceUseKind.Semantic,
                        rule.OriginNode ??
                        mapping.AnalysisContext.Registration.Syntax);
                }
            }
        }

        foreach (var slice in mappingSlices)
        {
            if (slice.CreateFailure is not null ||
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
                            Origin: not ConstructorParameterRuleOrigin.Omitted,
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
                            AddSourceUse(
                                sourceUses,
                                sourceMember,
                                SourceUseKind.Semantic,
                                rule.OriginNode ??
                                slice.AnalysisContext.Registration.Syntax);
                        }
                    }
                }
            }
        }

        var callbacks = configuration.Declarative.ResultPolicies
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
                sourceUses,
                sourceDiscards,
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
        }

        void AddFailureUncertainty(
            MappingFailureObservation? failure,
            ConstructorPlanningObservation? constructorObservation)
        {
            if (failure is null)
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

            if (failure.AffectedPath.Phase is
                MappingPlanPhase.Configuration or
                MappingPlanPhase.Transfer or
                MappingPlanPhase.ResultSelection or
                MappingPlanPhase.NestedMapping ||
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
                        AddUncertain(sourceMember);
                    }

                    if (rule.DestinationMember is { } destinationMember)
                    {
                        AddUncertain(destinationMember);
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
                     ? []
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

    private static ImmutableArray<ISymbol> DistinctSymbols(
        IEnumerable<ISymbol> symbols)
    {
        var result = ImmutableArray.CreateBuilder<ISymbol>();

        foreach (var symbol in symbols)
        {
            if (!result.Any(candidate =>
                    SymbolEqualityComparer.Default.Equals(
                        candidate,
                        symbol)))
            {
                result.Add(symbol);
            }
        }

        return result.ToImmutable();
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
        ImmutableArray<SourceUseObservation>.Builder sourceUses,
        ImmutableArray<SourceDiscardObservation>.Builder sourceDiscards,
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
                sourceDiscards.Add(
                    new SourceDiscardObservation(
                        discard.Member,
                        discard.Statement,
                        callback));
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
}
