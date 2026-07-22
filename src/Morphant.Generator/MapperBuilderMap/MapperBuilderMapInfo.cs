using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.MapperBuilderMap;

internal readonly record struct MapperBuilderMapInfo(
    MethodDeclarationSyntax ConfigureSyntax,
    ImmutableArray<MapperBuilderMapRegistrationInfo> Registrations);

internal readonly record struct MapperBuilderMapRegistrationInfo(
    InvocationExpressionSyntax Syntax,
    ITypeSymbol SourceType,
    ITypeSymbol DestinationType);
