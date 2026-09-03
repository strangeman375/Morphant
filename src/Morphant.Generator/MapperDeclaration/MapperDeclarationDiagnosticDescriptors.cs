using Microsoft.CodeAnalysis;
using Morphant.Generator.Diagnostics;

#pragma warning disable RS1032 // Messages are fixed by the public diagnostics catalog.

namespace Morphant.Generator.MapperDeclaration;

internal static class MapperDeclarationDiagnosticDescriptors
{
    private const string Category = "Morphant.Declaration";

    public static readonly DiagnosticDescriptor MissingTypeMapperBase =
        Create(
            "MORPH0005",
            "Mapper must derive from TypeMapper",
            "Mapper '{0}' must derive from " +
            "'Morphant.TypeMapper<{0}>'.");

    public static readonly DiagnosticDescriptor MapperMustBePartial =
        Create(
            "MORPH0006",
            "Mapper must be partial",
            "Mapper '{0}' must be declared partial.");

    public static readonly DiagnosticDescriptor ContainingTypeMustBePartial =
        Create(
            "MORPH0007",
            "Containing type must be partial",
            "Containing type '{0}' must be declared partial.");

    public static readonly DiagnosticDescriptor FileLocalType =
        Create(
            "MORPH0008",
            "File-local mapper declaration is not supported",
            "File-local type '{0}' cannot declare or contain a Morphant mapper.");

    public static readonly DiagnosticDescriptor ExactContract =
        Create(
            "MORPH0009",
            "Mapping is already implemented",
            "Mapping '{0}' is already implemented by mapper '{1}'. " +
            "Remove the interface declaration or the Map registration.");

    public static readonly DiagnosticDescriptor UnifiableContract =
        Create(
            "MORPH0010",
            "Mapping may conflict with a declared interface",
            "Mapper '{1}' declares an interface that may conflict with " +
            "generated mapping '{0}'.");

    public static readonly DiagnosticDescriptor SupportsConflict =
        Create(
            "MORPH0034",
            "Mapper member conflicts with generated Supports",
            "Mapper '{0}' declares 'Supports(System.Type, System.Type)', " +
            "which conflicts with the generated mapper.");

    public static readonly DiagnosticDescriptor InvalidSelfType =
        Create(
            "MORPH0058",
            "Mapper self type is invalid",
            "Mapper '{0}' must close 'Morphant.TypeMapper<TMapper>' with " +
            "its own type or a correctly constrained CRTP self type instead " +
            "of '{1}'.");

    public static readonly DiagnosticDescriptor InaccessibleMapperType =
        Create(
            "MORPH0059",
            "Mapper type is inaccessible to generated code",
            "Type '{0}' cannot declare or contain a Morphant mapper because " +
            "it is not accessible to generated namespace-level code.");

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
