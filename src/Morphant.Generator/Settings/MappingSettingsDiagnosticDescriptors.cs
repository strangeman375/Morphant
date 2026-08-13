using Microsoft.CodeAnalysis;
using Morphant.Generator.Diagnostics;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.Settings;

internal static class MappingSettingsDiagnosticDescriptors
{
    private const string Category = "Morphant.Settings";

    public static readonly DiagnosticDescriptor InvalidSettingValue = Create(
        "MORPH0021",
        "Invalid mapping setting value",
        "Setting '{0}' must be a supported compile-time constant.");

    public static readonly DiagnosticDescriptor InvalidMsBuildSettingValue =
        Create(
            "MORPH0022",
            "Invalid MSBuild mapping setting value",
            "MSBuild property '{0}' must use a supported value.");

    public static readonly DiagnosticDescriptor InapplicableSetting = Create(
        "MORPH0023",
        "Mapping setting is not applicable",
        "Setting '{0}' does not apply to {1} for mapping '{2}' " +
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
            helpLinkUri: DiagnosticHelpLink.For(id),
            customTags: []);
    }
}
