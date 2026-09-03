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
    ITypeSymbol? MapperSelfType,
    Location? InvalidSelfTypeLocation,
    bool HasMalformedBaseDeclaration,
    ClassDeclarationSyntax? MapperPartialIssue,
    bool AllMapperDeclarationsPartial,
    ImmutableArray<MapperContainingTypeIssue> ContainingPartialIssues,
    bool AllContainingDeclarationsPartial,
    ImmutableArray<MapperContainingTypeIssue> FileLocalIssues,
    ImmutableArray<MethodDeclarationSyntax> ConflictingSupportsMethods,
    CSharpCompilation Compilation)
{
    public bool HasMissingTypeMapperDiagnostic =>
        !DerivesFromTypeMapper && !HasMalformedBaseDeclaration;

    public bool HasInvalidSelfTypeDiagnostic =>
        InvalidSelfTypeLocation is not null;

    public bool CanGenerateExecutableArtifact =>
        DerivesFromTypeMapper &&
        !HasInvalidSelfTypeDiagnostic &&
        AllMapperDeclarationsPartial &&
        AllContainingDeclarationsPartial &&
        FileLocalIssues.IsEmpty &&
        ConflictingSupportsMethods.IsEmpty;

    public string MapperDisplayName =>
        MapperContractDisplay.CreateType(MapperType);

    public string MapperIdentity =>
        SymbolNameHelper.GetFullMetadataName(MapperType);

    public string MapperSelfTypeDisplayName =>
        MapperSelfType is null
            ? string.Empty
            : MapperContractDisplay.CreateType(MapperSelfType);
}

internal readonly record struct MapperContainingTypeIssue(
    INamedTypeSymbol Type,
    TypeDeclarationSyntax Declaration)
{
    public string DisplayName => MapperContractDisplay.CreateType(Type);
}
