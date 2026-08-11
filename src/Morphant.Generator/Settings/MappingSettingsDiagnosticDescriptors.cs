using Microsoft.CodeAnalysis;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.Settings;

internal static class MappingSettingsDiagnosticDescriptors
{
    private const string Category = "Morphant.Settings";

    public static readonly DiagnosticDescriptor InvalidSettingValue = Create(
        "MORPH0021",
        "Invalid mapping setting value",
        "Mapping setting '{0}' must be a supported compile-time constant.");

    public static readonly DiagnosticDescriptor InvalidMsBuildSettingValue =
        Create(
            "MORPH0022",
            "Invalid MSBuild mapping setting value",
            "MSBuild property '{0}' must name a supported mapping setting " +
            "value.");

    public static readonly DiagnosticDescriptor InapplicableSetting = Create(
        "MORPH0023",
        "Mapping setting is not applicable",
        "Mapping setting '{0}' is not applicable to {1} for contract '{2}' " +
        "in mapper '{3}'.");

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
