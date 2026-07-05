namespace Morphant.Generator.TemplateSurface;

public readonly record struct DestinationTypeReference
(
    string MetadataName,
    string FullyQualifiedName,
    string TemplateNamespace,
    string TemplateTypeName,
    string TemplateTypeFullyQualifiedName
);
