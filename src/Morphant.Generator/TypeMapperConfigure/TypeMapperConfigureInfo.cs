using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperConfigure;

internal readonly record struct TypeMapperConfigureInfo
(
    MethodDeclarationSyntax Syntax,
    IMethodSymbol Symbol
);
