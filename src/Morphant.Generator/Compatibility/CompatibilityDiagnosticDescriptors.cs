using Microsoft.CodeAnalysis;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.Compatibility;

internal static class CompatibilityDiagnosticDescriptors
{
    private const string Category = "Morphant.Compatibility";

    public static readonly DiagnosticDescriptor UnsupportedLanguageVersion =
        Create(
            "MORPH0001",
            "Unsupported C# language version",
            "Morphant requires C# 9.0 or later, but this compilation uses C# {0}.");

    public static readonly DiagnosticDescriptor RuntimeContractNotFound =
        Create(
            "MORPH0002",
            "Morphant runtime not found",
            "Morphant requires a reference to a compatible runtime library.");

    public static readonly DiagnosticDescriptor AmbiguousRuntimeContract =
        Create(
            "MORPH0003",
            "Multiple Morphant runtimes found",
            "Multiple Morphant runtime libraries were found. Reference exactly one.");

    public static readonly DiagnosticDescriptor IncompatibleRuntimeContract =
        Create(
            "MORPH0004",
            "Incompatible Morphant runtime",
            "The Morphant runtime is incompatible with this generator: {0}.");

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
