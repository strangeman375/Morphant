using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal readonly record struct MappingAnalysisContext(
    MappingPairRegistrationModel Registration,
    MappingPairIdentity Identity,
    INamedTypeSymbol TargetMapper)
{
    public ITypeSymbol SourceType => Registration.SourceType;

    public ITypeSymbol DestinationType => Registration.DestinationType;
}

[Flags]
internal enum MappingOperationSet
{
    None = 0,
    Create = 1 << 0,
    Update = 1 << 1,
    All = Create | Update
}

[Flags]
internal enum MappingExecutionPathSet
{
    None = 0,
    Create = 1 << 0,
    UpdateWithoutPrevious = 1 << 1,
    UpdateWithPrevious = 1 << 2,
    NoPrevious = Create | UpdateWithoutPrevious,
    Update = UpdateWithoutPrevious | UpdateWithPrevious,
    All = Create | Update
}

internal enum MappingPlanPhase
{
    Configuration,
    Transfer,
    ResultSelection,
    Construction,
    Members,
    NestedMapping
}

internal readonly record struct MappingAffectedPath(
    MappingExecutionPathSet Paths,
    MappingPlanPhase Phase,
    SyntaxNode? BranchOrigin = null)
{
    public MappingOperationSet Operations =>
        (Paths.HasFlag(MappingExecutionPathSet.Create)
            ? MappingOperationSet.Create
            : MappingOperationSet.None) |
        (Paths.HasFlag(MappingExecutionPathSet.UpdateWithoutPrevious) ||
         Paths.HasFlag(MappingExecutionPathSet.UpdateWithPrevious)
            ? MappingOperationSet.Update
            : MappingOperationSet.None);

    public static MappingAffectedPath All(MappingPlanPhase phase) =>
        new(MappingExecutionPathSet.All, phase);

    public static MappingAffectedPath Create(MappingPlanPhase phase) =>
        new(MappingExecutionPathSet.Create, phase);

    public static MappingAffectedPath NoPrevious(MappingPlanPhase phase) =>
        new(MappingExecutionPathSet.NoPrevious, phase);

    public static MappingAffectedPath Update(MappingPlanPhase phase) =>
        new(MappingExecutionPathSet.Update, phase);

    public static MappingAffectedPath ExistingDestination(
        MappingPlanPhase phase) =>
        new(MappingExecutionPathSet.UpdateWithPrevious, phase);
}

internal enum MappingObservationOriginKind
{
    Registration,
    MapperConfiguration,
    Setting,
    Callback,
    CompilerPreflight,
    Convention,
    Constructor,
    ConstructorParameter,
    Member,
    NestedMarker
}

internal enum MappingFailureReason
{
    UnsupportedMappingContract,
    InvalidBaseConfiguration,
    UnsupportedMapperBuilderFlow,
    UnsupportedMappingBuilderFlow,
    InvalidPairConfiguration,
    InvalidManualSetting,
    InvalidSetting,
    InapplicableSetting,
    CallbackCannotBeTransferred,
    UnsupportedRuntimeCallback,
    UnsupportedStructuredCallback,
    UnsupportedStructuredSyntax,
    StructuredResultRequiresDestination,
    MissingConstructionPolicy,
    ConstructorSelectionFailed,
    ConstructorParameterRuleInvalid,
    TerminalPreviousWithoutValue,
    TerminalNullConstruction,
    MemberRuleInvalid,
    MemberLifecycleInvalid,
    TerminalNullMembers,
    NestedPairUnknown,
    NestedResultIncompatible,
    NestedUpdateDestinationInvalid
}

internal sealed record MappingFailureObservation(
    MappingFailureReason Reason,
    string RecoveryMessage,
    MappingObservationOriginKind OriginKind,
    SyntaxNode OriginNode,
    SyntaxNode? OffendingNode,
    ISymbol? OffendingSymbol,
    Location PrimaryLocation,
    ImmutableArray<Location> AdditionalLocations,
    INamedTypeSymbol SourceMapper,
    MappingAnalysisContext Context,
    MappingAffectedPath AffectedPath,
    ImmutableArray<NestedMappingObservation> NestedObservations)
{
    public static MappingFailureObservation Create(
        MappingAnalysisContext context,
        MappingFailureReason reason,
        string recoveryMessage,
        MappingObservationOriginKind originKind,
        MappingAffectedPath affectedPath,
        SyntaxNode? originNode = null,
        INamedTypeSymbol? sourceMapper = null,
        SyntaxNode? offendingNode = null,
        ISymbol? offendingSymbol = null,
        Location? primaryLocation = null,
        ImmutableArray<Location> additionalLocations = default,
        ImmutableArray<NestedMappingObservation> nestedObservations = default)
    {
        var resolvedOrigin = originNode ?? context.Registration.Syntax;

        return new MappingFailureObservation(
            reason,
            recoveryMessage,
            originKind,
            resolvedOrigin,
            offendingNode,
            offendingSymbol,
            primaryLocation ??
            offendingNode?.GetLocation() ??
            resolvedOrigin.GetLocation(),
            additionalLocations.IsDefault ? ImmutableArray<Location>.Empty : additionalLocations,
            sourceMapper ?? context.TargetMapper,
            context,
            affectedPath,
            nestedObservations.IsDefault ? ImmutableArray<NestedMappingObservation>.Empty : nestedObservations);
    }
}

internal enum ConstructorCandidateRejectionReason
{
    None,
    StrategyShape,
    AmbiguousStrategy,
    AbstractDestination,
    RequiredMember,
    ResultDependentInitializer,
    MissingSourceMember,
    IncompatibleArgument,
    InvocationBinding,
    ExplicitRule
}

internal enum ConstructorParameterRuleOrigin
{
    Convention,
    Auto,
    Ignore,
    Value,
    Omitted
}

internal sealed record ConstructorParameterRuleObservation(
    IParameterSymbol? Parameter,
    string ParameterName,
    ConstructorParameterRuleOrigin Origin,
    SyntaxNode? OriginNode,
    ISymbol? SourceMember,
    ISymbol? DestinationMember,
    bool IsApplicable,
    ConstructorCandidateRejectionReason RejectionReason,
    SyntaxNode? DesignatorNode = null,
    ImmutableArray<ISymbol> SourcePathMembers = default);

internal sealed record ConstructorCandidateObservation(
    IMethodSymbol Constructor,
    ImmutableArray<ConstructorParameterRuleObservation> ParameterRules,
    ConstructorCandidateRejectionReason RejectionReason);

internal sealed record ConstructorPlanningObservation(
    ConstructorSelectionValue? Strategy,
    SyntaxNode? StrategyOrigin,
    ImmutableArray<ConstructorCandidateObservation> Candidates,
    IMethodSymbol? SelectedConstructor,
    ImmutableArray<StructuredTerminalObservation> Terminals,
    ImmutableArray<FlatteningIssueObservation> FlatteningIssues = default);

internal enum StructuredTerminalKind
{
    Previous,
    NullConstruction,
    NullMembers
}

internal sealed record StructuredTerminalObservation(
    StructuredTerminalKind Kind,
    SyntaxNode OriginNode,
    MappingAffectedPath AffectedPath,
    ImmutableArray<DeclarativeTerminalAliasSyntax> Aliases = default);

internal enum MemberRuleOrigin
{
    Convention,
    Auto,
    Ignore,
    ExplicitValue,
    NestedMapping,
    ConstructorArgument
}

internal enum MemberRuleInvalidReason
{
    None,
    AutoUnavailable,
    MarkerTargetMismatch,
    ImportedSlotHidden
}

[Flags]
internal enum MemberLifecycleDependency
{
    None = 0,
    Creation = 1 << 0,
    ExistingDestination = 1 << 1,
    Result = 1 << 2,
    InitOnly = 1 << 3
}

internal sealed record MemberRuleObservation(
    ISymbol DestinationMember,
    ISymbol? SourceMember,
    MemberRuleOrigin Origin,
    SyntaxNode? OriginNode,
    bool IsRequired,
    MemberLifecycleDependency Lifecycle,
    ISymbol? HiddenImportedSlot,
    MemberRuleInvalidReason InvalidReason = MemberRuleInvalidReason.None,
    ITypeSymbol? AssertedType = null,
    SyntaxNode? DesignatorNode = null,
    SyntaxNode? ResultDependencyOrigin = null,
    INamedTypeSymbol? SourceMapper = null,
    ITypeSymbol? TargetType = null,
    ImmutableArray<ISymbol> SourcePathMembers = default);

internal sealed record MemberPlanningObservation(
    ImmutableArray<ISymbol> SupportedSourceMembers,
    ImmutableArray<ISymbol> SupportedDestinationMembers,
    ImmutableArray<MemberRuleObservation> Rules,
    ImmutableArray<ISymbol> RequiredObligations,
    ImmutableArray<StructuredTerminalObservation> Terminals,
    ImmutableArray<NestedMappingObservation> NestedMappings = default,
    ImmutableArray<SourceDiscardObservation> SourceDiscards = default,
    SyntaxNode? PlanOrigin = null,
    ImmutableArray<FlatteningIssueObservation> FlatteningIssues = default);

internal sealed record FlatteningIssueObservation(
    string TargetName,
    ISymbol TargetSymbol,
    SyntaxNode? OriginNode,
    ImmutableArray<string> CandidatePaths,
    ImmutableArray<Location> CandidateLocations);

internal enum NestedDestinationOrigin
{
    None,
    Explicit,
    GeneratedCurrent,
    ReadOnlyProxy
}

internal enum NestedConversionStatus
{
    Unknown,
    Compatible,
    Incompatible
}

internal enum NestedMappingFailureKind
{
    None,
    SourceTypeUnknown,
    ParameterlessSourceUnavailable,
    DestinationTypeUnknown,
    ResultIncompatible,
    ExplicitDestinationIncompatible,
    ExplicitNullForNonNullableValue,
    AdaptiveCurrentUnavailable,
    AdaptiveCurrentIncompatible,
    AdaptiveCurrentAmbiguous,
    ReadOnlyProxyInvalid
}

internal sealed record NestedMappingObservation(
    InvocationExpressionSyntax Producer,
    IMethodSymbol ProducerSymbol,
    SyntaxNode? TerminalTarget,
    DeclarativeNestedMapOperation? Operation,
    ITypeSymbol? InferredSourceType,
    ITypeSymbol? InferredDestinationType,
    NestedConversionStatus ResultConversion,
    NestedDestinationOrigin DestinationOrigin,
    SyntaxNode? ExplicitDestination,
    ITypeSymbol? ExplicitDestinationType,
    string? GeneratedCurrentDestination,
    ISymbol? ReadOnlyProxy,
    ImmutableArray<string> AdaptiveLocalTargets,
    ImmutableArray<SyntaxNode> AdaptiveTargetDesignators,
    NestedMappingFailureKind FailureKind,
    ExpressionSyntax? SourceExpression,
    ITypeSymbol? TargetType,
    string? TargetName,
    ISymbol? TargetSymbol,
    SyntaxNode? TargetDesignator,
    ITypeSymbol? CurrentDestinationType,
    ISymbol? CurrentDestinationSymbol,
    INamedTypeSymbol SourceMapper,
    MappingExecutionPathSet Paths);

internal enum SourceUseKind
{
    Semantic,
    Potential
}

internal sealed record SourceUseObservation(
    ISymbol Member,
    SourceUseKind Kind,
    SyntaxNode OriginNode);

internal sealed record SourceDiscardObservation(
    ISymbol Member,
    ExpressionStatementSyntax Statement,
    BoundConfigurationExpression Callback);

internal sealed record DestinationOccupancyObservation(
    ISymbol Member,
    MemberRuleOrigin Origin,
    SyntaxNode? OriginNode);

internal sealed record CompletenessPlanningObservation(
    ImmutableArray<ISymbol> SupportedSourceMembers,
    ImmutableArray<ISymbol> SupportedDestinationMembers,
    ImmutableArray<SourceUseObservation> SourceUses,
    ImmutableArray<SourceDiscardObservation> SourceDiscards,
    ImmutableArray<DestinationOccupancyObservation> DestinationOccupancy,
    ImmutableArray<ISymbol> ErrorDerivedUncertainty);
