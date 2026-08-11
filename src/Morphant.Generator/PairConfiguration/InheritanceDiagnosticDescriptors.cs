using Microsoft.CodeAnalysis;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.PairConfiguration;

internal static class InheritanceDiagnosticDescriptors
{
    private const string Category = "Morphant.Inheritance";

    public static readonly DiagnosticDescriptor DuplicateBaseConfiguration =
        Create(
            "MORPH0024",
            "Duplicate base configuration call",
            "Base configuration is included more than once in Configure " +
            "of mapper '{0}'.");

    public static readonly DiagnosticDescriptor DuplicateIncludeBase = Create(
        "MORPH0025",
        "Duplicate IncludeBase call",
        "IncludeBase is configured more than once for contract '{0}' in " +
        "mapper '{1}'.");

    public static readonly DiagnosticDescriptor IncludedPairNotFound = Create(
        "MORPH0026",
        "Included mapping pair not found",
        "Included mapping contract '{0}' was not found for contract '{1}' " +
        "in mapper '{2}'.");

    public static readonly DiagnosticDescriptor IncompatibleIncludedType =
        Create(
            "MORPH0027",
            "Included mapping type is incompatible",
            "Current {0} type '{1}' is not assignable to included {0} " +
            "type '{2}' for contract '{3}' in mapper '{4}'.");

    public static readonly DiagnosticDescriptor InaccessibleInheritedCallback =
        Create(
            "MORPH0028",
            "Inherited mapping callback is inaccessible",
            "Inherited {0} callback for contract '{1}' cannot be accessed " +
            "from mapper '{2}'.");

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
