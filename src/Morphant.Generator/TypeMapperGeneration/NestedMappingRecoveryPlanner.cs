using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class NestedMappingRecoveryPlanner
{
    private const string RecoveryMessage =
        "This nested mapping is invalid.";

    public static TypeMapperMappingModel Apply(
        TypeMapperMappingModel mapping,
        INamedTypeSymbol mapperType)
    {
        if (mapping.ManualMapping is not null ||
            mapping.Failure is { } mappingFailure &&
            !IsNestedFailure(mappingFailure.Reason))
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
                        MappingExecutionPathSet.NoPrevious,
                        createRoot: true,
                        mapperType),
                    ApplyNode(
                        controlFlow.UpdateRoot,
                        MappingExecutionPathSet.UpdateWithPrevious,
                        createRoot: false,
                        mapperType))
            };
        }

        var result = ApplyFlatPath(
            mapping,
            MappingExecutionPathSet.NoPrevious,
            createPath: true,
            mapperType);
        return ApplyFlatPath(
            result,
            MappingExecutionPathSet.UpdateWithPrevious,
            createPath: false,
            mapperType);
    }

    private static TypeMapperControlFlowNode ApplyNode(
        TypeMapperControlFlowNode node,
        MappingExecutionPathSet paths,
        bool createRoot,
        INamedTypeSymbol mapperType)
    {
        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            return node with
            {
                EvaluationContinuation = ApplyNode(
                    evaluationContinuation,
                    paths,
                    createRoot,
                    mapperType)
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
                                paths,
                                createRoot,
                                mapperType)
                        })
                    .ToImmutableArray(),
                SwitchContinuation = node.SwitchContinuation is
                    { } continuation
                        ? ApplyNode(
                            continuation,
                            paths,
                            createRoot,
                            mapperType)
                        : null
            };
        }

        if (node.Condition is not null)
        {
            return node with
            {
                WhenTrue = ApplyNode(
                    node.WhenTrue!,
                    paths,
                    createRoot,
                    mapperType),
                WhenFalse = ApplyNode(
                    node.WhenFalse!,
                    paths,
                    createRoot,
                    mapperType)
            };
        }

        return node.Leaf is { } leaf
            ? node with
            {
                Leaf = ApplyFlatPath(
                    leaf,
                    paths,
                    createRoot,
                    mapperType)
            }
            : node;
    }

    private static TypeMapperMappingModel ApplyFlatPath(
        TypeMapperMappingModel mapping,
        MappingExecutionPathSet paths,
        bool createPath,
        INamedTypeSymbol mapperType)
    {
        if (mapping.PostMemberControlFlow is { } postMemberControlFlow)
        {
            mapping = mapping with
            {
                PostMemberControlFlow = ApplyMemberNode(
                    mapping,
                    postMemberControlFlow,
                    paths)
            };
        }

        var observations = CollectInvalidObservations(mapping, paths);

        if (observations.IsEmpty)
        {
            return mapping;
        }

        var currentFailure = createPath
            ? mapping.CreateFailure
            : mapping.UpdateFailure;

        if (!CanReplace(mapping, currentFailure))
        {
            return mapping;
        }

        var failure = BuildFailure(mapping, observations, paths);

        if (createPath && mapping.CreateFactory is not null)
        {
            return MemberRecoveryPlanner.ApplyPostResultFailure(
                mapping,
                failure,
                mapperType);
        }

        return createPath
            ? mapping with { CreateFailure = failure }
            : mapping with { UpdateFailure = failure };
    }

    private static TypeMapperMemberControlFlowNode ApplyMemberNode(
        TypeMapperMappingModel mapping,
        TypeMapperMemberControlFlowNode node,
        MappingExecutionPathSet paths)
    {
        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            return node with
            {
                EvaluationContinuation = ApplyMemberNode(
                    mapping,
                    evaluationContinuation,
                    paths)
            };
        }

        if (node.SwitchExpression is not null)
        {
            return node with
            {
                SwitchSections = node.SwitchSections.Select(section =>
                        section with
                        {
                            Branch = ApplyMemberNode(
                                mapping,
                                section.Branch,
                                paths)
                        })
                    .ToImmutableArray(),
                SwitchContinuation = node.SwitchContinuation is
                    { } continuation
                        ? ApplyMemberNode(
                            mapping,
                            continuation,
                            paths)
                        : null
            };
        }

        if (node.Condition is not null)
        {
            return node with
            {
                WhenTrue = ApplyMemberNode(
                    mapping,
                    node.WhenTrue!,
                    paths),
                WhenFalse = ApplyMemberNode(
                    mapping,
                    node.WhenFalse!,
                    paths)
            };
        }

        var observations = CollectInvalidObservations(
            node.MemberObservation?.NestedMappings ?? ImmutableArray<NestedMappingObservation>.Empty,
            paths);

        if (observations.IsEmpty ||
            node.Failure is { } currentFailure &&
            !IsNestedFailure(currentFailure.Reason))
        {
            return node;
        }

        return node with
        {
            MemberMappings = ImmutableArray<TypeMapperMemberMappingModel>.Empty,
            Failure = BuildFailure(mapping, observations, paths)
        };
    }

    private static ImmutableArray<NestedMappingObservation>
        CollectInvalidObservations(
            TypeMapperMappingModel mapping,
            MappingExecutionPathSet paths)
    {
        var result = ImmutableArray.CreateBuilder<NestedMappingObservation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        Add(mapping.NestedObservations);
        Add(mapping.MemberObservation?.NestedMappings ?? ImmutableArray<NestedMappingObservation>.Empty);
        Add(mapping.CreateFailure?.NestedObservations ?? ImmutableArray<NestedMappingObservation>.Empty);
        Add(mapping.UpdateFailure?.NestedObservations ?? ImmutableArray<NestedMappingObservation>.Empty);
        Add(mapping.Failure?.NestedObservations ?? ImmutableArray<NestedMappingObservation>.Empty);

        return result.ToImmutable();

        void Add(ImmutableArray<NestedMappingObservation> observations)
        {
            if (observations.IsDefaultOrEmpty)
            {
                return;
            }

            foreach (var observation in observations)
            {
                if (observation.FailureKind ==
                        NestedMappingFailureKind.None ||
                    (observation.Paths & paths) ==
                        MappingExecutionPathSet.None)
                {
                    continue;
                }

                var key = ObservationKey(observation);

                if (seen.Add(key))
                {
                    result.Add(observation);
                }
            }
        }
    }

    private static ImmutableArray<NestedMappingObservation>
        CollectInvalidObservations(
            ImmutableArray<NestedMappingObservation> observations,
            MappingExecutionPathSet paths)
    {
        if (observations.IsDefaultOrEmpty)
        {
            return ImmutableArray<NestedMappingObservation>.Empty;
        }

        var result = ImmutableArray.CreateBuilder<
            NestedMappingObservation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var observation in observations)
        {
            if (observation.FailureKind ==
                    NestedMappingFailureKind.None ||
                (observation.Paths & paths) ==
                    MappingExecutionPathSet.None)
            {
                continue;
            }

            var key = ObservationKey(observation);

            if (seen.Add(key))
            {
                result.Add(observation);
            }
        }

        return result.ToImmutable();
    }

    private static string ObservationKey(
        NestedMappingObservation observation)
    {
        var target = observation.TargetDesignator ??
            observation.TerminalTarget;
        var explicitDestination = observation.ExplicitDestination;

        return observation.Producer.SyntaxTree.FilePath + "|" +
               observation.Producer.SpanStart + "|" +
               observation.Producer.Span.Length + "|" +
               (int)observation.FailureKind + "|" +
               (target?.SyntaxTree.FilePath ?? string.Empty) + "|" +
               (target?.SpanStart ?? -1) + "|" +
               (target?.Span.Length ?? 0) + "|" +
               (explicitDestination?.SpanStart ?? -1) + "|" +
               (explicitDestination?.Span.Length ?? 0) + "|" +
               observation.TargetName;
    }

    private static bool CanReplace(
        TypeMapperMappingModel mapping,
        MappingFailureObservation? current)
    {
        if (current is null || IsNestedFailure(current.Reason))
        {
            return true;
        }

        if (current.Reason !=
                MappingFailureReason.ConstructorSelectionFailed ||
            mapping.ConstructorObservation is not { } construction)
        {
            return false;
        }

        return construction.Candidates.Any(static candidate =>
            candidate.RejectionReason ==
                ConstructorCandidateRejectionReason.RequiredMember);
    }

    private static MappingFailureObservation BuildFailure(
        TypeMapperMappingModel mapping,
        ImmutableArray<NestedMappingObservation> observations,
        MappingExecutionPathSet paths)
    {
        var first = observations[0];

        return MappingFailureObservation.Create(
            mapping.AnalysisContext,
            ToFailureReason(first.FailureKind),
            RecoveryMessage,
            MappingObservationOriginKind.NestedMarker,
            new MappingAffectedPath(
                paths,
                MappingPlanPhase.NestedMapping,
                first.Producer),
            first.Producer,
            offendingNode: first.Producer,
            offendingSymbol: first.ProducerSymbol,
            nestedObservations: observations);
    }

    private static MappingFailureReason ToFailureReason(
        NestedMappingFailureKind kind)
    {
        return kind switch
        {
            NestedMappingFailureKind.SourceTypeUnknown or
            NestedMappingFailureKind.ParameterlessSourceUnavailable or
            NestedMappingFailureKind.DestinationTypeUnknown =>
                MappingFailureReason.NestedPairUnknown,
            NestedMappingFailureKind.ResultIncompatible =>
                MappingFailureReason.NestedResultIncompatible,
            _ => MappingFailureReason.NestedUpdateDestinationInvalid
        };
    }

    private static bool IsNestedFailure(MappingFailureReason reason)
    {
        return reason is
            MappingFailureReason.NestedPairUnknown or
            MappingFailureReason.NestedResultIncompatible or
            MappingFailureReason.NestedUpdateDestinationInvalid;
    }
}
