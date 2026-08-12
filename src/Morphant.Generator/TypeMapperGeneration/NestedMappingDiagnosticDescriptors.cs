using Microsoft.CodeAnalysis;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.TypeMapperGeneration;

internal static class NestedMappingDiagnosticDescriptors
{
    private const string Category = "Morphant.NestedMapping";

    public static readonly DiagnosticDescriptor PairUnknown = Create(
        "MORPH0044",
        "Nested mapping pair cannot be determined",
        "Nested mapping pair for marker '{0}' in contract '{1}' cannot be " +
        "determined: {2}. Reachable paths: {3}.");

    public static readonly DiagnosticDescriptor ResultIncompatible = Create(
        "MORPH0045",
        "Nested mapping result is incompatible",
        "Nested mapping result type '{0}' in contract '{1}' cannot be " +
        "converted warning-free to target type '{2}'. Reachable paths: " +
        "{3}.");

    public static readonly DiagnosticDescriptor UpdateDestinationInvalid =
        Create(
            "MORPH0046",
            "Nested Update destination is invalid",
            "Nested Update destination for marker '{0}' in contract '{1}' " +
            "is invalid: {2}. Reachable paths: {3}.");

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
