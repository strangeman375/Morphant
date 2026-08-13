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
            "Mapping '{0}' cannot create a destination. Affected cases: {1}.");

    public static readonly DiagnosticDescriptor ConventionUnavailable =
        Create(
            "MORPH0036",
            "Constructor cannot be selected",
            "ConstructorSelection.{1} cannot select a constructor for " +
            "mapping '{0}': {2}.");

    public static readonly DiagnosticDescriptor InvalidParameterRule =
        Create(
            "MORPH0037",
            "Constructor parameter rule is invalid",
            "Rule for constructor parameter '{0}' is invalid in mapping " +
            "'{1}': {2}.");

    public static readonly DiagnosticDescriptor PreviousUnavailable =
        Create(
            "MORPH0038",
            "Previous destination is unavailable",
            "'previous' is unavailable in mapping '{0}'. Affected cases: " +
            "{1}.");

    public static readonly DiagnosticDescriptor NullConstructionPlan =
        Create(
            "MORPH0039",
            "Construct or Resolve returned no destination",
            "Construct or Resolve returned null or default for mapping " +
            "'{0}'. Affected cases: {1}.");

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
