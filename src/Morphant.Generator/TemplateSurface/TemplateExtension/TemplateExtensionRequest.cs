namespace Morphant.Generator.TemplateSurface.TemplateExtension;

public readonly record struct TemplateExtensionRequest
(
    DestinationTypeReference DestinationType,
    string HintName
);
