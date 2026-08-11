using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TypeMapperRuntimeEquality
{
    public static bool AreEquivalent(
        TypeMapperControlFlowNode left,
        TypeMapperControlFlowNode right) =>
        Equals(Normalize(left), Normalize(right));

    public static bool AreEquivalent(
        TypeMapperConstructorMappingModel left,
        TypeMapperConstructorMappingModel right) =>
        Equals(Normalize(left), Normalize(right));

    private static TypeMapperControlFlowNode Normalize(
        TypeMapperControlFlowNode node)
    {
        return node with
        {
            WhenTrue = node.WhenTrue is { } whenTrue
                ? Normalize(whenTrue)
                : null,
            WhenFalse = node.WhenFalse is { } whenFalse
                ? Normalize(whenFalse)
                : null,
            Leaf = node.Leaf is { } leaf
                ? Normalize(leaf)
                : null,
            SwitchSections = node.SwitchSections.IsDefault
                ? default
                : node.SwitchSections.Select(section =>
                        section with
                        {
                            Branch = Normalize(section.Branch)
                        })
                    .ToImmutableArray(),
            SwitchContinuation = node.SwitchContinuation is
                { } switchContinuation
                    ? Normalize(switchContinuation)
                    : null,
            EvaluationContinuation = node.EvaluationContinuation is
                { } evaluationContinuation
                    ? Normalize(evaluationContinuation)
                    : null
        };
    }

    private static TypeMapperMappingModel Normalize(
        TypeMapperMappingModel mapping)
    {
        return mapping with
        {
            CreateConstructor = mapping.CreateConstructor is
                { } constructor
                    ? Normalize(constructor)
                    : null,
            ControlFlow = mapping.ControlFlow is { } controlFlow
                ? new TypeMapperControlFlowMappingModel(
                    Normalize(controlFlow.CreateRoot),
                    Normalize(controlFlow.UpdateRoot))
                : null,
            CreateFailure = Normalize(
                mapping.CreateFailure,
                mapping.AnalysisContext),
            UpdateFailure = Normalize(
                mapping.UpdateFailure,
                mapping.AnalysisContext),
            CreateOperationFailure = Normalize(
                mapping.CreateOperationFailure,
                mapping.AnalysisContext),
            UpdateOperationFailure = Normalize(
                mapping.UpdateOperationFailure,
                mapping.AnalysisContext),
            Failure = Normalize(
                mapping.Failure,
                mapping.AnalysisContext),
            PostMemberControlFlow = mapping.PostMemberControlFlow is
                { } postMemberControlFlow
                    ? Normalize(
                        postMemberControlFlow,
                        mapping.AnalysisContext)
                    : null,
            ConstructorObservation = null,
            MemberObservation = null,
            NestedObservations = default,
            CompletenessObservation = null,
            StructuredTerminals = default
        };
    }

    private static TypeMapperConstructorMappingModel Normalize(
        TypeMapperConstructorMappingModel constructor)
    {
        return constructor with
        {
            Arguments = constructor.Arguments.Select(argument =>
                    argument with
                    {
                        ParameterSymbol = null,
                        SourceMemberSymbol = null,
                        RuleOriginNode = null,
                        RuleOrigin = null
                    })
                .ToImmutableArray()
        };
    }

    private static TypeMapperMemberControlFlowNode Normalize(
        TypeMapperMemberControlFlowNode node,
        MappingAnalysisContext context)
    {
        return node with
        {
            WhenTrue = node.WhenTrue is { } whenTrue
                ? Normalize(whenTrue, context)
                : null,
            WhenFalse = node.WhenFalse is { } whenFalse
                ? Normalize(whenFalse, context)
                : null,
            Failure = Normalize(node.Failure, context),
            SwitchSections = node.SwitchSections.IsDefault
                ? default
                : node.SwitchSections.Select(section =>
                        section with
                        {
                            Branch = Normalize(section.Branch, context)
                        })
                    .ToImmutableArray(),
            SwitchContinuation = node.SwitchContinuation is
                { } switchContinuation
                    ? Normalize(switchContinuation, context)
                    : null,
            EvaluationContinuation = node.EvaluationContinuation is
                { } evaluationContinuation
                    ? Normalize(evaluationContinuation, context)
                    : null
        };
    }

    private static MappingFailureObservation? Normalize(
        MappingFailureObservation? failure,
        MappingAnalysisContext context)
    {
        if (failure is null)
        {
            return null;
        }

        return new MappingFailureObservation(
            Reason: default,
            RecoveryMessage: failure.RecoveryMessage,
            OriginKind: default,
            OriginNode: context.Registration.Syntax,
            OffendingNode: null,
            OffendingSymbol: null,
            PrimaryLocation: Location.None,
            AdditionalLocations: [],
            SourceMapper: context.TargetMapper,
            Context: context,
            AffectedPath: default,
            NestedObservations: []);
    }
}
