using System.Collections.Immutable;

namespace Morphant.Generator.TemplateSurface.TemplateType;

internal sealed record TemplateTypeModel(
    string TemplateNamespace,
    string TemplateTypeName,
    string DestinationTypeName,
    ImmutableArray<TemplateTypeParameterModel> TypeParameters,
    bool CanConstructDestination,
    TemplateDocumentationModel DestinationDocumentation,
    ImmutableArray<TemplateConstructorModel> Constructors,
    ImmutableArray<TemplateConstructorFieldModel> ConstructorFields,
    ImmutableArray<TemplateMemberModel> Members)
{
    public string ConstructorMembersTypeName =>
        TemplateTypeName + "ConstructorMembers";
}

internal sealed record TemplateTypeParameterModel(
    string Name,
    ImmutableArray<string> Constraints);

internal sealed record TemplateDocumentationModel(
    string Cref,
    bool HasDocumentation);

internal sealed record TemplateConstructorModel(
    ImmutableArray<TemplateConstructorParameterModel> Parameters);

internal sealed record TemplateConstructorParameterModel(
    string Name,
    string TypeName,
    string TypeSuffix,
    bool IsOptional,
    string? DefaultValueDisplay);

internal sealed record TemplateConstructorFieldModel(
    string Name,
    string ParameterName,
    string TypeName);

internal sealed record TemplateMemberModel(
    string Name,
    string TypeName,
    TemplateDocumentationModel Documentation,
    bool RequiresNullableAnnotationsDisabled = false,
    string? ObsoleteAttributeSource = null);
