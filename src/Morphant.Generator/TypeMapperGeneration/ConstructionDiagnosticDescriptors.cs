using Microsoft.CodeAnalysis;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ConstructionDiagnosticDescriptors
{
    private const string Category = "Morphant.Construction";

    public static readonly DiagnosticDescriptor MissingConstructionPolicy =
        Create(
            "MORPH0035",
            "Destination construction is not configured",
            "Destination construction for contract '{0}' is not configured " +
            "for reachable paths: {1}.");

    public static readonly DiagnosticDescriptor ConventionUnavailable =
        Create(
            "MORPH0036",
            "Convention construction is unavailable",
            "Convention construction for contract '{0}' is unavailable with " +
            "ConstructorSelection.{1}: {2}.");

    public static readonly DiagnosticDescriptor InvalidParameterRule =
        Create(
            "MORPH0037",
            "Constructor parameter rule is invalid",
            "Constructor parameter rule for '{0}' in contract '{1}' is " +
            "invalid: {2}.");

    public static readonly DiagnosticDescriptor PreviousUnavailable =
        Create(
            "MORPH0038",
            "Previous destination is unavailable",
            "Previous destination is unavailable for contract '{0}' on " +
            "reachable paths: {1}.");

    public static readonly DiagnosticDescriptor NullConstructionPlan =
        Create(
            "MORPH0039",
            "Structured construction plan is null",
            "Structured construction plan for contract '{0}' cannot be null " +
            "on reachable paths: {1}.");

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
