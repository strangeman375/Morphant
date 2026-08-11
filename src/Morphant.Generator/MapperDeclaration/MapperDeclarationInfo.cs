using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.MapperDeclaration;

internal sealed record MapperDeclarationInfo(
    ClassDeclarationSyntax AttributedDeclaration,
    AttributeSyntax Attribute,
    INamedTypeSymbol MapperType,
    bool DerivesFromTypeMapper,
    bool HasMalformedBaseDeclaration,
    ClassDeclarationSyntax? MapperPartialIssue,
    bool AllMapperDeclarationsPartial,
    ImmutableArray<MapperContainingTypeIssue> ContainingPartialIssues,
    bool AllContainingDeclarationsPartial,
    ImmutableArray<MapperContainingTypeIssue> FileLocalIssues,
    ImmutableArray<MethodDeclarationSyntax> ConflictingSupportsMethods)
{
    public bool HasMissingTypeMapperDiagnostic =>
        !DerivesFromTypeMapper && !HasMalformedBaseDeclaration;

    public bool CanGenerateExecutableArtifact =>
        DerivesFromTypeMapper &&
        AllMapperDeclarationsPartial &&
        AllContainingDeclarationsPartial &&
        FileLocalIssues.IsEmpty &&
        ConflictingSupportsMethods.IsEmpty;

    public string MapperDisplayName => MapperType.ToDisplayString(
        SymbolDisplayFormats.FullyQualifiedNullable);

    public string MapperIdentity =>
        SymbolNameHelper.GetFullMetadataName(MapperType);
}

internal readonly record struct MapperContainingTypeIssue(
    INamedTypeSymbol Type,
    TypeDeclarationSyntax Declaration)
{
    public string DisplayName => Type.ToDisplayString(
        SymbolDisplayFormats.FullyQualifiedNullable);
}
