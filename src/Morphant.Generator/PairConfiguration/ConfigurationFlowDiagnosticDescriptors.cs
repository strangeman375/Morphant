using Microsoft.CodeAnalysis;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.PairConfiguration;

internal static class ConfigurationFlowDiagnosticDescriptors
{
    private const string Category = "Morphant.Configuration";

    public static readonly DiagnosticDescriptor MissingConfigure = Create(
        "MORPH0015",
        "Mapper must declare Configure",
        "Mapper '{0}' must declare a source-bodied override of " +
        "'Configure(Morphant.MapperBuilder)'.");

    public static readonly DiagnosticDescriptor UnavailableBaseConfigure =
        Create(
            "MORPH0016",
            "Base mapper configuration is unavailable",
            "The Configure body for base mapper '{0}' is unavailable while " +
            "analyzing mapper '{1}'.");

    public static readonly DiagnosticDescriptor UnsupportedMapperFlow =
        Create(
            "MORPH0017",
            "Unsupported mapper builder flow",
            "Mapper builder flow in Configure of mapper '{0}' cannot be " +
            "analyzed by Morphant.");

    public static readonly DiagnosticDescriptor UnsupportedMappingFlow =
        Create(
            "MORPH0018",
            "Unsupported mapping builder flow",
            "Mapping builder flow for contract '{0}' in mapper '{1}' " +
            "cannot be analyzed by Morphant.");

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
