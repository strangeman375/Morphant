using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Morphant.Generator.MappingPair;
using Morphant.Generator.Settings;

namespace Morphant.Generator.PairConfiguration;

internal readonly record struct MapperPairConfigurationModel(
    MapperMappingPairModel MappingPairs,
    PairConfigurationSettings RootSettings,
    ImmutableArray<PairConfigurationModel> Pairs);

internal readonly record struct PairConfigurationModel(
    MappingPairModel Pair,
    PairConfigurationSettings Settings,
    DeclarativePairConfigurationModel Declarative,
    ManualPairConfigurationModel Manual,
    PairConfigurationCompositionModel Composition,
    PairConfigurationConflict Conflicts);

internal readonly record struct DeclarativePairConfigurationModel(
    ImmutableArray<ConstructConfigurationModel> Constructs,
    ImmutableArray<MembersConfigurationModel> Members);

internal readonly record struct ManualPairConfigurationModel(
    ImmutableArray<ConvertConfigurationModel> Conversions);

internal readonly record struct ConstructConfigurationModel(
    InvocationExpressionSyntax Invocation,
    ConstructConfigurationForm Form,
    BoundConfigurationExpression Expression);

internal readonly record struct MembersConfigurationModel(
    InvocationExpressionSyntax Invocation,
    MembersConfigurationForm Form,
    BoundConfigurationExpression Expression);

internal readonly record struct ConvertConfigurationModel(
    InvocationExpressionSyntax Invocation,
    BoundConfigurationExpression Expression);

internal sealed record BoundConfigurationExpression(
    ExpressionSyntax Syntax,
    SemanticModel SemanticModel,
    IOperation? Operation,
    INamedTypeSymbol DelegateType,
    IMethodSymbol DelegateInvokeMethod);

internal readonly record struct PairConfigurationCompositionModel(
    ImmutableArray<InvocationExpressionSyntax> IncludeBaseCalls)
{
    // IncludeBase becomes discoverable when mapper composition is introduced.
    // Keeping it in the model now avoids changing every downstream plan shape.
    public static PairConfigurationCompositionModel Empty =>
        new([]);
}

internal readonly record struct PairConfigurationSettings(
    PairConfigurationSetting<MappingModeValue> MappingMode,
    PairConfigurationSetting<NullSourceHandlingValue> NullSourceHandling,
    PairConfigurationSetting<NullDestinationHandlingValue>
        NullDestinationHandling,
    PairConfigurationSetting<ConstructorSelectionValue> ConstructorSelection,
    PairConfigurationSetting<MemberSelectionValue> MemberSelection,
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

internal enum ConstructConfigurationForm
{
    Source,
    SourceAndPrevious
}

internal enum MembersConfigurationForm
{
    SourceAndPrevious,
    SourcePreviousAndResult
}

[Flags]
internal enum PairConfigurationConflict
{
    None = 0,
    DuplicateConstruct = 1 << 0,
    DuplicateMembers = 1 << 1,
    DuplicateConvert = 1 << 2,
    MixedManualAndDeclarative = 1 << 3
}
