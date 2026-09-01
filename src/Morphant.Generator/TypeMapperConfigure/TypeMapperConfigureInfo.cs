using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperDeclaration;

namespace Morphant.Generator.TypeMapperConfigure;

internal readonly record struct TypeMapperConfigureInfo(
    MethodDeclarationSyntax Syntax,
    INamedTypeSymbol MapperType,
    MapperDeclarationInfo? Declaration);
