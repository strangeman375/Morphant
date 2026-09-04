using Microsoft.CodeAnalysis;
using Morphant.Generator.Diagnostics;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.MappingPair;

internal static class MappingRegistrationDiagnosticDescriptors
{
    private const string Category = "Morphant.Registration";

    public static readonly DiagnosticDescriptor UnavailableMappingType =
        Create(
            "MORPH0011",
            "Mapping type is inaccessible",
            "The {0} type '{1}' is not accessible to the generated mapper.");

    public static readonly DiagnosticDescriptor UnsupportedMappingRoot =
        Create(
            "MORPH0012",
            "Unsupported mapping type",
            "The {0} type '{1}' cannot be used in Map because it is {2}.");

    public static readonly DiagnosticDescriptor DuplicateRegistration =
        Create(
            "MORPH0013",
            "Duplicate mapping registration",
            "Mapping '{0}' is registered more than once in mapper " +
            "'{1}'.");

    public static readonly DiagnosticDescriptor UnifiableContracts =
        Create(
            "MORPH0014",
            "Mappings may become identical",
            "Mappings '{0}' and '{1}' may become identical for some generic " +
            "type arguments in mapper '{2}'.");

    public static readonly DiagnosticDescriptor ConflictingTuplePresentation =
        Create(
            "MORPH0056",
            "Tuple presentation is conflicting",
            "Mapping '{0}' uses tuple presentation '{1}', which conflicts " +
            "with the presentation '{2}' of the same underlying mapping pair.");

    public static readonly DiagnosticDescriptor
        MapperFamilyParameterMissingFromPair =
            Create(
                "MORPH0060",
                "Mapper family parameter is absent from mapping",
                "Mapper family type parameter '{0}' must occur in the " +
                "source or destination type of mapping '{1}'.");

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
