using Microsoft.CodeAnalysis;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.PairConfiguration;

internal static class MappingCompositionDiagnosticDescriptors
{
    private const string Category = "Morphant.Composition";

    public static readonly DiagnosticDescriptor DuplicatePlanSlot = Create(
        "MORPH0019",
        "Duplicate mapping plan slot",
        "Mapping plan slot '{0}' is configured more than once for contract " +
        "'{1}' in mapper '{2}'.");

    public static readonly DiagnosticDescriptor MixedConvertAndDeclarative =
        Create(
            "MORPH0020",
            "Convert cannot be combined with result policy or Members",
            "Convert cannot be combined with a result policy or Members " +
            "for contract '{0}' in mapper '{1}'.");

    private static DiagnosticDescriptor Create(
        string id,
        string title,
        string messageFormat)
    {
        return new DiagnosticDescriptor(
            id,
            title,
            messageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: null,
            helpLinkUri: null,
            customTags: []);
    }
}
