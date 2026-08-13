using Microsoft.CodeAnalysis;
using Morphant.Generator.Diagnostics;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.PairConfiguration;

internal static class ConfigurationFlowDiagnosticDescriptors
{
    private const string Category = "Morphant.Configuration";

    public static readonly DiagnosticDescriptor MissingConfigure = Create(
        "MORPH0015",
        "Mapper must declare Configure",
        "Mapper '{0}' must override 'Configure(Morphant.MapperBuilder)' " +
        "with a readable method body.");

    public static readonly DiagnosticDescriptor UnavailableBaseConfigure =
        Create(
            "MORPH0016",
            "Base mapper configuration is unavailable",
            "Morphant cannot read Configure for base mapper '{0}' while " +
            "analyzing mapper '{1}'.");

    public static readonly DiagnosticDescriptor UnsupportedMapperFlow =
        Create(
            "MORPH0017",
            "Configure cannot be analyzed",
            "Morphant cannot analyze Configure in mapper '{0}'.");

    public static readonly DiagnosticDescriptor UnsupportedMappingFlow =
        Create(
            "MORPH0018",
            "Mapping configuration cannot be analyzed",
            "Morphant cannot analyze configuration for mapping '{0}' in " +
            "mapper '{1}'.");

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
