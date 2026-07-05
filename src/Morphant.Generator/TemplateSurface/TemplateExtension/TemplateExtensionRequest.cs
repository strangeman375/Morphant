namespace Morphant.Generator.TemplateSurface.TemplateExtension;

public readonly record struct TemplateExtensionRequest
(
    TemplateDestinationTypeInfo TemplateDestinationType,
    string HintName
);
