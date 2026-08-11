using Microsoft.CodeAnalysis;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.MapperDeclaration;

internal static class MapperDeclarationDiagnosticDescriptors
{
    private const string Category = "Morphant.Declaration";

    public static readonly DiagnosticDescriptor MissingTypeMapperBase =
        Create(
            "MORPH0005",
            "Mapper must derive from TypeMapper",
            "Mapper '{0}' must derive from 'Morphant.TypeMapper'.");

    public static readonly DiagnosticDescriptor MapperMustBePartial =
        Create(
            "MORPH0006",
            "Mapper must be partial",
            "Mapper '{0}' must be declared partial so Morphant can " +
            "generate its mapping contract.");

    public static readonly DiagnosticDescriptor ContainingTypeMustBePartial =
        Create(
            "MORPH0007",
            "Containing type must be partial",
            "Containing type '{0}' must be declared partial so Morphant " +
            "can generate nested mapper contracts.");

    public static readonly DiagnosticDescriptor FileLocalType =
        Create(
            "MORPH0008",
            "File-local mapper declaration is not supported",
            "File-local type '{0}' cannot declare or contain a generated " +
            "Morphant mapper contract.");

    public static readonly DiagnosticDescriptor ExactContract =
        Create(
            "MORPH0009",
            "Mapping contract is already declared",
            "Mapping contract '{0}' is already declared by mapper '{1}'. " +
            "Remove the interface declaration or the Map registration.");

    public static readonly DiagnosticDescriptor UnifiableContract =
        Create(
            "MORPH0010",
            "Mapping contract conflicts with a declared interface",
            "Mapping contract '{0}' can unify with an interface contract " +
            "declared by mapper '{1}'.");

    public static readonly DiagnosticDescriptor SupportsConflict =
        Create(
            "MORPH0034",
            "Mapper member conflicts with generated Supports",
            "Mapper '{0}' declares 'Supports(System.Type, System.Type)', " +
            "which conflicts with the Morphant-generated mapping contract.");

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
