using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.MapperDeclaration;

internal sealed record MapperDeclarationInfo(
    ClassDeclarationSyntax AttributedDeclaration,
    AttributeSyntax Attribute,
    INamedTypeSymbol MapperType,
    bool DerivesFromTypeMapper,
    MapperSelfTypeIssue? InvalidSelfTypeIssue,
    bool HasMalformedBaseDeclaration,
    ClassDeclarationSyntax? MapperPartialIssue,
    bool AllMapperDeclarationsPartial,
    ImmutableArray<MapperContainingTypeIssue> ContainingPartialIssues,
    bool AllContainingDeclarationsPartial,
    ImmutableArray<MapperContainingTypeIssue> FileLocalIssues,
    ImmutableArray<MapperContainingTypeIssue> InaccessibleTypeIssues,
    ImmutableArray<MethodDeclarationSyntax> ConflictingSupportsMethods,
    CSharpCompilation Compilation)
{
    public bool HasMissingTypeMapperDiagnostic =>
        !DerivesFromTypeMapper && !HasMalformedBaseDeclaration;

    public bool HasInvalidSelfTypeDiagnostic =>
        InvalidSelfTypeIssue is not null;

    public bool CanGenerateExecutableArtifact =>
        DerivesFromTypeMapper &&
        !HasInvalidSelfTypeDiagnostic &&
        AllMapperDeclarationsPartial &&
        AllContainingDeclarationsPartial &&
        FileLocalIssues.IsEmpty &&
        InaccessibleTypeIssues.IsEmpty &&
        ConflictingSupportsMethods.IsEmpty;

    public string MapperDisplayName =>
        MapperContractDisplay.CreateType(MapperType);

    public string MapperIdentity =>
        SymbolNameHelper.GetFullMetadataName(MapperType);
}

internal readonly record struct MapperSelfTypeIssue(
    INamedTypeSymbol MapperType,
    ITypeSymbol SelfType,
    Location Location)
{
    public string MapperDisplayName =>
        MapperContractDisplay.CreateType(MapperType);

    public string SelfTypeDisplayName =>
        MapperContractDisplay.CreateType(SelfType);
}

internal readonly record struct MapperContainingTypeIssue(
    INamedTypeSymbol Type,
    TypeDeclarationSyntax Declaration)
{
    public string DisplayName => MapperContractDisplay.CreateType(Type);
}
