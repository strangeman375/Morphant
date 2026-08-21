using Microsoft.CodeAnalysis;
using Morphant.Generator.Diagnostics;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.PairConfiguration;

internal static class PolymorphismDiagnosticDescriptors
{
    private const string Category = "Morphant.Polymorphism";

    public static readonly DiagnosticDescriptor SelfLink = Create(
        "MORPH0052",
        "Polymorphic mapping cannot link to itself",
        "ForDerived source type '{0}' is the exact source type of mapping " +
        "'{1}'.");

    public static readonly DiagnosticDescriptor DuplicateSource = Create(
        "MORPH0053",
        "Polymorphic source branch is duplicated",
        "ForDerived source type '{0}' is configured more than once for " +
        "mapping '{1}'.");

    public static readonly DiagnosticDescriptor IncompatibleType = Create(
        "MORPH0054",
        "Polymorphic branch type is incompatible",
        "ForDerived {0} type '{1}' is not assignable to base {0} type " +
        "'{2}' for mapping '{3}'.");

    public static readonly DiagnosticDescriptor InaccessibleType = Create(
        "MORPH0055",
        "Polymorphic branch type is inaccessible",
        "ForDerived {0} type '{1}' is inaccessible from generated mapper " +
        "'{2}'.");

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
