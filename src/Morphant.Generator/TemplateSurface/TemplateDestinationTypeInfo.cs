namespace Morphant.Generator.TemplateSurface;

public readonly record struct TemplateDestinationTypeInfo
(
    TemplateDestinationTypeKind Kind,
    TemplateTypeDefinitionInfo? TemplateTypeDefinition,
    string UsageIdentity,
    string FullyQualifiedName,
    string ExistingDestinationTypeFullyQualifiedName,
    string TemplateResultTypeFullyQualifiedName,
    bool CanGenerateTemplateExtension
);

public enum TemplateDestinationTypeKind
{
    GeneratedTemplate,
    DirectTemplate
}

public readonly record struct TemplateTypeDefinitionInfo
(
    string MetadataName,
    string TemplateNamespace,
    string TemplateTypeName
);
