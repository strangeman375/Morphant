using Microsoft.CodeAnalysis;
using Morphant.Generator.Diagnostics;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.PairConfiguration;

internal static class InheritanceDiagnosticDescriptors
{
    private const string Category = "Morphant.Inheritance";

    public static readonly DiagnosticDescriptor DuplicateBaseConfiguration =
        Create(
            "MORPH0024",
            "Duplicate base configuration call",
            "Base configuration is included more than once in mapper " +
            "'{0}'.");

    public static readonly DiagnosticDescriptor DuplicateIncludeBase = Create(
        "MORPH0025",
        "Duplicate IncludeBase call",
        "IncludeBase is configured more than once for mapping '{0}' in " +
        "mapper '{1}'.");

    public static readonly DiagnosticDescriptor IncludedPairNotFound = Create(
        "MORPH0026",
        "Included mapping pair not found",
        "Included mapping '{0}' was not found for mapping '{1}' " +
        "in mapper '{2}'.");

    public static readonly DiagnosticDescriptor IncompatibleIncludedType =
        Create(
            "MORPH0027",
            "Included mapping type is incompatible",
            "The {0} type '{1}' is not compatible with included {0} type " +
            "'{2}' for mapping '{3}' in mapper '{4}'.");

    public static readonly DiagnosticDescriptor InaccessibleInheritedCallback =
        Create(
            "MORPH0028",
            "Inherited mapping expression is inaccessible",
            "The inherited {0} expression for mapping '{1}' is inaccessible " +
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
            helpLinkUri: DiagnosticHelpLink.For(id),
            customTags: []);
    }
}
