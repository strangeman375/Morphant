using Microsoft.CodeAnalysis;
using Morphant.Generator.Diagnostics;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.TypeMapperGeneration;

internal static class NestedMappingDiagnosticDescriptors
{
    private const string Category = "Morphant.NestedMapping";

    public static readonly DiagnosticDescriptor PairUnknown = Create(
        "MORPH0044",
        "Nested mapping types cannot be determined",
        "Cannot determine source or destination type for '{0}' in mapping " +
        "'{1}': {2}. Affected cases: {3}.");

    public static readonly DiagnosticDescriptor ResultIncompatible = Create(
        "MORPH0045",
        "Nested mapping result is incompatible",
        "Nested mapping result type '{0}' cannot be assigned to '{2}' in " +
        "mapping '{1}'. Affected cases: {3}.");

    public static readonly DiagnosticDescriptor UpdateDestinationInvalid =
        Create(
            "MORPH0046",
            "Nested Update destination is invalid",
            "Destination for nested '{0}' is invalid in mapping '{1}': " +
            "{2}. Affected cases: {3}.");

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
            helpLinkUri: DiagnosticHelpLink.For(id),
            customTags: []);
    }
}
