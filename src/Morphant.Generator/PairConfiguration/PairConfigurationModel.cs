using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.Settings;

namespace Morphant.Generator.PairConfiguration;

internal readonly record struct MapperPairConfigurationModel(
    MapperDeclarationInfo Declaration,
    MapperMappingPairModel MappingPairs,
    ImmutableArray<MapperMappingPairModel> SurfaceMappingPairs,
    PairConfigurationSettings RootSettings,
    ImmutableArray<PairConfigurationSettings> BaseRootSettings,
    ImmutableArray<DuplicateBaseConfigurationCallModel>
        DuplicateBaseConfigurationCalls,
    ImmutableArray<PairConfigurationModel> Pairs,
    bool HasInvalidBaseConfiguration,
    ImmutableArray<UnavailableBaseConfigurationModel>
        UnavailableBaseConfigurations,
    ImmutableArray<BuilderFlowBreakModel> FlowBreaks)
{
    public bool HasMapperWideConfigurationFlowFailure =>
        !UnavailableBaseConfigurations.IsEmpty ||
        FlowBreaks.Any(static flowBreak =>
            flowBreak.Kind == BuilderFlowBreakKind.Mapper);
}

internal readonly record struct PairConfigurationModel(
    MappingPairModel Pair,
    PairConfigurationOriginModel Origin,
    ImmutableArray<MappingPlanSlotOccurrenceModel> LocalPlanSlots,
    PairConfigurationSettings Settings,
    DeclarativePairConfigurationModel Declarative,
    ManualPairConfigurationModel Manual,
    PolymorphicPairConfigurationModel Polymorphism,
    PairConfigurationCompositionModel Composition,
    PairConfigurationConflict Conflicts);

internal readonly record struct MappingPlanSlotOccurrenceModel(
    InvocationExpressionSyntax Invocation,
    MappingPlanSlotKind Kind);

internal enum MappingPlanSlotKind
{
    ResultPolicy,
    Members,
    IncludeMembers,
    Convert
}

internal readonly record struct DeclarativePairConfigurationModel(
    ImmutableArray<ResultPolicyConfigurationModel> ResultPolicies,
    ImmutableArray<MembersConfigurationModel> Members,
    ImmutableArray<IncludeMembersConfigurationModel> IncludeMembers);

internal readonly record struct ManualPairConfigurationModel(
    ImmutableArray<ConvertConfigurationModel> Conversions);

internal readonly record struct ResultPolicyConfigurationModel(
    InvocationExpressionSyntax Invocation,
    ResultPolicyKind Kind,
    ResultPolicyForm Form,
    BoundConfigurationExpression Expression);

internal readonly record struct MembersConfigurationModel(
    InvocationExpressionSyntax Invocation,
    MembersConfigurationForm Form,
    BoundConfigurationExpression Expression);

internal readonly record struct IncludeMembersConfigurationModel(
    InvocationExpressionSyntax Invocation,
    ITypeSymbol SourceType,
    BoundConfigurationExpression Expression);

internal readonly record struct ConvertConfigurationModel(
    InvocationExpressionSyntax Invocation,
    ConvertConfigurationForm Form,
    BoundConfigurationExpression Expression);

internal readonly record struct DerivedMappingConfigurationModel(
    InvocationExpressionSyntax Invocation,
    ITypeSymbol SourceType,
    ITypeSymbol DestinationType,
    bool HasValidMethodBinding);

internal readonly record struct PolymorphicPairConfigurationModel(
    ImmutableArray<DerivedMappingConfigurationModel> DerivedMappings,
    ImmutableArray<PolymorphicConfigurationIssueModel> Issues)
{
    public static PolymorphicPairConfigurationModel Empty =>
        new(
            ImmutableArray<DerivedMappingConfigurationModel>.Empty,
            ImmutableArray<PolymorphicConfigurationIssueModel>.Empty);
}

internal readonly record struct PolymorphicConfigurationIssueModel(
    PolymorphicConfigurationIssueKind Kind,
    DerivedMappingConfigurationModel DerivedMapping,
    InvocationExpressionSyntax? FirstInvocation = null);

internal enum PolymorphicConfigurationIssueKind
{
    SelfLink,
    DuplicateSource,
    IncompatibleSource,
    IncompatibleDestination,
    InaccessibleSource,
    InaccessibleDestination
}

internal sealed record BoundConfigurationExpression(
    ExpressionSyntax Syntax,
    SemanticModel SemanticModel,
    IOperation? Operation,
    INamedTypeSymbol DelegateType,
    IMethodSymbol DelegateInvokeMethod,
    INamedTypeSymbol DeclaringMapperType,
    int DeclaringLevelOrder,
    ImmutableArray<Location> InaccessibleReferenceLocations)
{
    public bool IsAccessibleFromTargetMapper =>
        InaccessibleReferenceLocations.IsEmpty;
}

internal readonly record struct PairConfigurationOriginModel(
    INamedTypeSymbol DeclaringMapperType,
    INamedTypeSymbol ConstructedMapperType,
    MappingPairRegistrationModel Registration,
    MappingPairRegistrationModel DeclaredRegistration,
    int LevelOrder);

internal sealed record DuplicateBaseConfigurationCallModel(
    INamedTypeSymbol DeclaringMapperType,
    InvocationExpressionSyntax FirstInvocation,
    InvocationExpressionSyntax DuplicateInvocation,
    int LevelOrder);

internal readonly record struct IncludeBaseConfigurationModel(
    InvocationExpressionSyntax Invocation,
    ITypeSymbol SourceType,
    ITypeSymbol DestinationType);

internal readonly record struct PairConfigurationCompositionModel(
    ImmutableArray<IncludeBaseConfigurationModel> IncludeBaseCalls,
    ImmutableArray<PairConfigurationSettings> IncludedBaseSettings,
    ImmutableArray<InheritanceCompositionIssueModel> Issues,
    ImmutableArray<InheritedCallbackAccessibilityModel>
        InaccessibleCallbacks)
{
    public static PairConfigurationCompositionModel Empty =>
        new(
            ImmutableArray<IncludeBaseConfigurationModel>.Empty,
            ImmutableArray<PairConfigurationSettings>.Empty,
            ImmutableArray<InheritanceCompositionIssueModel>.Empty,
            ImmutableArray<InheritedCallbackAccessibilityModel>.Empty);
}

internal sealed record InheritanceCompositionIssueModel(
    InheritanceCompositionIssueKind Kind,
    PairConfigurationOriginModel Origin,
    IncludeBaseConfigurationModel IncludeBase,
    InvocationExpressionSyntax? FirstInvocation = null);

internal enum InheritanceCompositionIssueKind
{
    DuplicateIncludeBase,
    MissingIncludedPair,
    IncompatibleSource,
    IncompatibleDestination,
    InvalidIncludedPair
}

internal sealed record InheritedCallbackAccessibilityModel(
    string CallbackName,
    InvocationExpressionSyntax Invocation,
    int LevelOrder,
    ImmutableArray<Location> ReferenceLocations);

internal readonly record struct PairConfigurationSettings(
    PairConfigurationSetting<MappingModeValue> MappingMode,
    PairConfigurationSetting<NullSourceHandlingValue> NullSourceHandling,
    PairConfigurationSetting<NullDestinationHandlingValue>
        NullDestinationHandling,
    PairConfigurationSetting<UnknownDerivedTypeHandlingValue>
        UnknownDerivedTypeHandling,
    PairConfigurationSetting<ConstructorSelectionValue> ConstructorSelection,
    PairConfigurationSetting<MemberSelectionValue> MemberSelection,
    PairConfigurationSetting<FlatteningValue> Flattening,
    PairConfigurationSetting<UnmappedMemberValidationValue>
        UnmappedMemberValidation)
{
    public static PairConfigurationSettings Empty => default;
}

internal readonly record struct PairConfigurationSetting<TValue>(
    SyntaxNode? Syntax,
    TValue? Value,
    PairConfigurationSettingOrigin Origin)
    where TValue : struct, Enum;

internal enum PairConfigurationSettingOrigin
{
    Unset,
    Implicit,
    Explicit
}

internal enum ResultPolicyKind
{
    Construct,
    Resolve,
    ConstructUsing,
    ResolveUsing
}

internal enum ResultPolicyForm
{
    Source,
    SourceAndContext,
    SourceAndPrevious,
    SourcePreviousAndContext
}

internal enum MembersConfigurationForm
{
    Source,
    SourceAndPrevious,
    SourcePreviousAndResult,
    SourcePreviousResultAndContext
}

internal enum ConvertConfigurationForm
{
    Source,
    SourceAndPrevious,
    SourcePreviousAndContext
}

[Flags]
internal enum PairConfigurationConflict
{
    None = 0,
    DuplicateResultPolicy = 1 << 0,
    DuplicateMembers = 1 << 1,
    DuplicateConvert = 1 << 2,
    MixedManualAndDeclarative = 1 << 3,
    DuplicateIncludeBase = 1 << 4,
    MissingBaseConfiguration = 1 << 5,
    MissingBasePair = 1 << 6,
    IncompatibleBasePair = 1 << 7,
    InaccessibleInheritedPlan = 1 << 8,
    InvalidBasePair = 1 << 9,
    DuplicateDerivedMapping = 1 << 10,
    InvalidDerivedMapping = 1 << 11
}
