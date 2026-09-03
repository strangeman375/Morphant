using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class MappingTypeNormalization
{
    public static ITypeSymbol NormalizeDynamic(
        ITypeSymbol type,
        Compilation compilation)
    {
        return type is IDynamicTypeSymbol
            ? compilation.GetSpecialType(SpecialType.System_Object)
            : type;
    }

    public static ITypeSymbol NormalizeDeclarativeSource(
        ITypeSymbol type,
        Compilation compilation,
        bool normalizeDynamic = true)
    {
        if (normalizeDynamic)
        {
            type = NormalizeDynamic(type, compilation);
        }

        if (IsNullableValue(type))
        {
            return ((INamedTypeSymbol)type).TypeArguments[0];
        }

        return type.IsReferenceType
            ? type.WithNullableAnnotation(
                NullableAnnotation.NotAnnotated)
            : type;
    }

    public static ITypeSymbol NormalizeManualSource(
        ITypeSymbol type,
        Compilation compilation,
        bool normalizeDynamic = true)
    {
        if (normalizeDynamic)
        {
            type = NormalizeDynamic(type, compilation);
        }

        return type.IsReferenceType
            ? type.WithNullableAnnotation(
                NullableAnnotation.Annotated)
            : type;
    }

    public static ITypeSymbol NormalizePreviousDestination(
        ITypeSymbol type,
        Compilation compilation,
        bool normalizeDynamic = true)
    {
        if (normalizeDynamic)
        {
            type = NormalizeDynamic(type, compilation);
        }

        if (IsNullableValue(type))
        {
            return ((INamedTypeSymbol)type).TypeArguments[0];
        }

        return type.IsReferenceType
            ? type.WithNullableAnnotation(
                NullableAnnotation.NotAnnotated)
            : type;
    }

    public static bool IsNullableValue(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.SpecialType ==
                   SpecialType.System_Nullable_T;
    }
}
