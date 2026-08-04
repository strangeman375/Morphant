using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.Settings;

namespace Morphant.Generator.MapperBuilderMap;

internal readonly record struct MapperBuilderMapInfo(
    MethodDeclarationSyntax ConfigureSyntax,
    MappingSettings Settings,
    ImmutableArray<MapperBuilderMapRegistrationInfo> Registrations);

internal readonly record struct MapperBuilderMapRegistrationInfo(
    InvocationExpressionSyntax Syntax,
    MapperBuilderMapTemplateInfo Templates,
    ITypeSymbol SourceType,
    ITypeSymbol DestinationType,
    MappingSettings Settings);

internal readonly record struct MapperBuilderMapTemplateInfo(
    InvocationExpressionSyntax? SourceTemplateSyntax,
    InvocationExpressionSyntax? DestinationTemplateSyntax,
    bool HasDuplicateSourceTemplate,
    bool HasDuplicateDestinationTemplate)
{
    public bool HasDuplicate =>
        HasDuplicateSourceTemplate ||
        HasDuplicateDestinationTemplate;
}
