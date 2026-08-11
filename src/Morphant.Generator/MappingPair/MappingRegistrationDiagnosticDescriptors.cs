using Microsoft.CodeAnalysis;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.MappingPair;

internal static class MappingRegistrationDiagnosticDescriptors
{
    private const string Category = "Morphant.Registration";

    public static readonly DiagnosticDescriptor UnavailableMappingType =
        Create(
            "MORPH0011",
            "Mapping type is unavailable to generated code",
            "The {0} type '{1}' is unavailable to Morphant-generated code.");

    public static readonly DiagnosticDescriptor UnsupportedMappingRoot =
        Create(
            "MORPH0012",
            "Unsupported mapping root type",
            "The {0} type '{1}' is not supported as a mapping root because " +
            "it is {2}.");

    public static readonly DiagnosticDescriptor DuplicateRegistration =
        Create(
            "MORPH0013",
            "Duplicate mapping registration",
            "Mapping contract '{0}' is registered more than once in mapper " +
            "'{1}'.");

    public static readonly DiagnosticDescriptor UnifiableContracts =
        Create(
            "MORPH0014",
            "Mapping contracts can unify",
            "Mapping contracts '{0}' and '{1}' can unify in mapper '{2}'.");

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
