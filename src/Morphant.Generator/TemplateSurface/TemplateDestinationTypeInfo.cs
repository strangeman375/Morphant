namespace Morphant.Generator.TemplateSurface;

public readonly record struct TemplateDestinationTypeInfo
(
    TemplateDestinationTypeKind Kind,
    TemplateTypeDefinitionInfo? TemplateTypeDefinition,
    string UsageIdentity,
    string FullyQualifiedName,
    string TemplateResultTypeFullyQualifiedName
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
