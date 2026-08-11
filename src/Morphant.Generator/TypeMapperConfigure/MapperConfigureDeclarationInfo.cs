using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperDeclaration;

namespace Morphant.Generator.TypeMapperConfigure;

internal sealed record MapperConfigureDeclarationInfo(
    MapperDeclarationInfo Declaration,
    MethodDeclarationSyntax? Syntax,
    MapperConfigureDeclarationState State)
{
    public Location MissingConfigureLocation =>
        Syntax?.Identifier.GetLocation() ??
        Declaration.AttributedDeclaration.Identifier.GetLocation();
}

internal enum MapperConfigureDeclarationState
{
    SourceBody,
    Missing,
    Bodyless,
    CompilerOwnedInvalid
}
