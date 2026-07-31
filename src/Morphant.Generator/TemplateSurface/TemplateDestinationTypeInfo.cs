namespace Morphant.Generator.TemplateSurface;

public readonly record struct TemplateDestinationTypeInfo
(
    TemplateDestinationTypeKind Kind,
    TemplateTypeDefinitionInfo? TemplateTypeDefinition,
    TemplateExtensionSignatureInfo SourceTypeSignature,
    TemplateExtensionSignatureInfo TemplateExtensionSignature,
    string SourceTypeFullyQualifiedName,
    string UsageIdentity,
    string FullyQualifiedName,
    string NonNullDestinationTypeFullyQualifiedName,
    string TemplateResultTypeFullyQualifiedName,
    bool CanGenerateTemplateExtension,
    bool CanGeneratePairSpecificTemplateExtension
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
    DirectTemplate,
    None
}

public readonly record struct TemplateTypeDefinitionInfo
(
    string MetadataName,
    string TemplateNamespace,
    string TemplateTypeName
);
