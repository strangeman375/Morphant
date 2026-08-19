using Microsoft.CodeAnalysis;
using Morphant.Generator.Diagnostics;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.TypeMapperGeneration;

internal static class FlatteningDiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor AmbiguousPath = new(
        "MORPH0051",
        "Flattened source path is ambiguous",
        "Auto flattening is ambiguous for mapping '{0}' in mapper '{1}': " +
        "{2}. Configure the target explicitly.",
        "Morphant.Flattening",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: null,
        helpLinkUri: DiagnosticHelpLink.For("MORPH0051"),
        customTags: []);
}
