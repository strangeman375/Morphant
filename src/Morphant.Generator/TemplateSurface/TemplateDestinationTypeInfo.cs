namespace Morphant.Generator.TemplateSurface;

public readonly record struct TemplateDestinationTypeInfo
(
    TemplateDestinationTypeKind Kind,
    TemplateTypeDefinitionInfo? TemplateTypeDefinition,
    TemplateExtensionSignatureInfo TemplateExtensionSignature,
    string UsageIdentity,
    string FullyQualifiedName,
    string ExistingDestinationTypeFullyQualifiedName,
    string TemplateResultTypeFullyQualifiedName,
    bool CanGenerateTemplateExtension
);

public readonly record struct TemplateExtensionSignatureInfo
(
    string Identity,
    int DynamicTypeCount,
    int NullableReferenceTypeCount,
    int ExplicitTupleElementNameCount
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
