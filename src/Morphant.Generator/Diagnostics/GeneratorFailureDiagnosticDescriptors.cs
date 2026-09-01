using Microsoft.CodeAnalysis;

namespace Morphant.Generator.Diagnostics;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

internal static class GeneratorFailureDiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor UnexpectedFailure =
        new(
            "MORPH0057",
            "Morphant generator failed unexpectedly",
            "Morphant generator {0} failed unexpectedly in stage '{1}': " +
            "{2}: {3}. Full exception details are available in generated " +
            "file '{4}'.",
            "Morphant.Generator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: null,
            helpLinkUri: DiagnosticHelpLink.For("MORPH0057"),
            customTags: []);
}
