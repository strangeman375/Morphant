using Microsoft.CodeAnalysis;
using Morphant.Generator.Diagnostics;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.TypeMapperGeneration;

internal static class CallbackDiagnosticDescriptors
{
    private const string Category = "Morphant.Callbacks";

    public static readonly DiagnosticDescriptor StructuredCallbackMustBeLambda =
        Create(
            "MORPH0029",
            "Mapping expression must be an inline lambda",
            "{0} for mapping '{1}' must use an inline lambda.");

    public static readonly DiagnosticDescriptor CallbackCannotBeTransferred =
        Create(
            "MORPH0030",
            "Mapping expression is unavailable",
            "{0} for mapping '{1}' cannot be used by mapper '{2}': {3}.");

    public static readonly DiagnosticDescriptor UnsupportedStructuredSyntax =
        Create(
            "MORPH0031",
            "Unsupported mapping expression",
            "{0} for mapping '{1}' contains unsupported syntax '{2}'.");

    public static readonly DiagnosticDescriptor StructuredInputIsReadOnly =
        Create(
            "MORPH0032",
            "Destination input is read-only",
            "'{0}' is read-only in mapping '{1}'.");

    public static readonly DiagnosticDescriptor InvalidCompileTimeMarkerUse =
        Create(
            "MORPH0033",
            "Invalid mapping method use",
            "'{0}' must be used directly inside Construct, Resolve, or " +
            "Members for mapping '{2}'.");

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
