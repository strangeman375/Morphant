using System.Collections.Immutable;

namespace Morphant.Generator.MemberSurface.MemberPlan;

internal sealed record MemberPlanModel(
    string Namespace,
    string TypeName,
    ImmutableArray<MemberPlanTypeParameterModel> TypeParameters,
    string DestinationCref,
    string? ObsoleteAttributeSource,
    ImmutableArray<MemberPlanPropertyModel> Members);

internal sealed record MemberPlanTypeParameterModel(
    string Name,
    ImmutableArray<string> Constraints,
    bool RequiresNullableAnnotationsDisabled);

internal sealed record MemberPlanPropertyModel(
    string Name,
    string TypeName,
    string Cref,
    bool CanWrite,
    bool AcceptsNull,
    bool RequiresNullableAnnotationsDisabled,
    string? ObsoleteAttributeSource);
