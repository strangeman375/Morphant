using Microsoft.CodeAnalysis;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.TypeMapperGeneration;

internal static class CallbackDiagnosticDescriptors
{
    private const string Category = "Morphant.Callbacks";

    public static readonly DiagnosticDescriptor StructuredCallbackMustBeLambda =
        Create(
            "MORPH0029",
            "Structured callback must be a lambda",
            "Structured {0} callback for contract '{1}' must be an inline lambda.");

    public static readonly DiagnosticDescriptor CallbackCannotBeTransferred =
        Create(
            "MORPH0030",
            "Callback cannot be transferred",
            "{0} callback for contract '{1}' cannot be transferred to " +
            "generated mapper '{2}': {3}.");

    public static readonly DiagnosticDescriptor UnsupportedStructuredSyntax =
        Create(
            "MORPH0031",
            "Unsupported structured callback syntax",
            "Structured {0} callback for contract '{1}' contains " +
            "unsupported syntax '{2}'.");

    public static readonly DiagnosticDescriptor StructuredInputIsReadOnly =
        Create(
            "MORPH0032",
            "Structured destination input is read-only",
            "Structured destination input '{0}' for contract '{1}' is " +
            "read-only and cannot be mutated.");

    public static readonly DiagnosticDescriptor InvalidCompileTimeMarkerUse =
        Create(
            "MORPH0033",
            "Invalid compile-time marker use",
            "Compile-time marker '{0}' cannot be used as a runtime value or " +
            "outside a supported terminal DSL position in {1} callback for " +
            "contract '{2}'.");

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
