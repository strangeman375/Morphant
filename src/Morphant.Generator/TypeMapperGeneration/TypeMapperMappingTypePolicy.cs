using System.Text;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TypeMapperMappingTypePolicy
{
    private static readonly SymbolDisplayFormat FullyQualifiedRuntime = new(
        globalNamespaceStyle:
            SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle:
            SymbolDisplayTypeQualificationStyle
                .NameAndContainingTypesAndNamespaces,
        genericsOptions:
            SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static string GetGeneratedTypeName(
        ITypeSymbol type)
    {
        var builder = new StringBuilder();

        foreach (var part in type.ToDisplayParts(
                     SymbolDisplayFormats.FullyQualifiedNullable))
        {
            builder.Append(
                part.Symbol is IDynamicTypeSymbol ||
                part.Kind == SymbolDisplayPartKind.Keyword &&
                part.ToString() == "dynamic"
                    ? "object"
                    : part.ToString());
        }

        return builder.ToString();
    }

    public static string GetGeneratedMaybeNullTypeName(
        ITypeSymbol type)
    {
        if (!type.IsValueType)
        {
            type = type.WithNullableAnnotation(
                NullableAnnotation.Annotated);
        }

        return GetGeneratedTypeName(type);
    }

    public static string GetGeneratedRuntimeTypeName(ITypeSymbol type)
    {
        var builder = new StringBuilder();

        foreach (var part in type.ToDisplayParts(FullyQualifiedRuntime))
        {
            builder.Append(
                part.Symbol is IDynamicTypeSymbol ||
                part.Kind == SymbolDisplayPartKind.Keyword &&
                part.ToString() == "dynamic"
                    ? "object"
                    : part.ToString());
        }

        return builder.ToString();
    }

    public static string GetGeneratedNonNullDestinationTypeName(
        ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType ==
                SpecialType.System_Nullable_T)
        {
            type = namedType.TypeArguments[0];
        }
        else if (!type.IsValueType)
        {
            type = type.WithNullableAnnotation(
                NullableAnnotation.NotAnnotated);
        }

        return GetGeneratedTypeName(type);
    }

}
