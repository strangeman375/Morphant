using Microsoft.CodeAnalysis;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MappingCompletenessDiagnosticDescriptors
{
    private const string Category = "Morphant.MappingCompleteness";

    public static readonly DiagnosticDescriptor SourceMemberUnused = Create(
        "MORPH0047",
        "Source member is not used",
        "Source member '{0}' is not used by mapping '{1}'.");

    public static readonly DiagnosticDescriptor DestinationMemberUnmapped =
        Create(
            "MORPH0048",
            "Destination member is not mapped",
            "Destination member '{0}' is not mapped by mapping '{1}'.");

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
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: null,
            helpLinkUri: null,
            customTags: []);
    }
}
