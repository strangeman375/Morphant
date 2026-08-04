using System.Collections.Immutable;

namespace Morphant.Generator.ConstructionSurface.ConstructionPlan;

internal sealed record ConstructionPlanModel(
    string Namespace,
    string TypeName,
    string ConstructorParametersTypeName,
    string DestinationTypeName,
    ImmutableArray<ConstructionTypeParameterModel> TypeParameters,
    ConstructionDocumentationModel DestinationDocumentation,
    string? ObsoleteAttributeSource,
    ImmutableArray<ConstructionConstructorModel> Constructors,
    ImmutableArray<ConstructionConstructorParameterFieldModel>
        ConstructorParameterFields);

internal sealed record ConstructionTypeParameterModel(
    string Name,
    ImmutableArray<string> Constraints,
    bool RequiresNullableAnnotationsDisabled);

internal sealed record ConstructionDocumentationModel(
    string Cref,
    bool HasDocumentation);

internal sealed record ConstructionConstructorModel(
    ConstructionDocumentationModel Documentation,
    string? ObsoleteAttributeSource,
    ImmutableArray<ConstructionConstructorParameterModel> Parameters);

internal sealed record ConstructionConstructorParameterModel(
    string Name,
    string TypeName,
    string TypeSuffix,
    bool IsOptional,
    string? DefaultValueDisplay,
    bool AcceptsNull,
    bool RequiresNullableAnnotationsDisabled);

internal sealed record ConstructionConstructorParameterFieldModel(
    string Name,
    string ParameterName,
    string TypeName,
    bool AcceptsNull,
    bool RequiresNullableAnnotationsDisabled);
