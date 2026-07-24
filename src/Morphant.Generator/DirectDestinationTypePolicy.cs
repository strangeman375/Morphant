using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal static class DirectDestinationTypePolicy
{
    public static bool IsDirect(INamedTypeSymbol destinationType)
    {
        if (IsCSharpPredefinedType(destinationType) ||
            destinationType.TypeKind == TypeKind.Enum ||
            IsSupportedBclDirectType(destinationType))
        {
            return true;
        }

        return IsNullableValueType(destinationType) &&
               destinationType.TypeArguments[0] is
                   INamedTypeSymbol underlyingType &&
               IsDirect(underlyingType);
    }

    private static bool IsCSharpPredefinedType(INamedTypeSymbol type)
    {
        return type.SpecialType is
            SpecialType.System_Object or
            SpecialType.System_String or
            SpecialType.System_Boolean or
            SpecialType.System_Char or
            SpecialType.System_SByte or
            SpecialType.System_Byte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_IntPtr or
            SpecialType.System_UIntPtr or
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_Decimal;
    }

    private static bool IsNullableValueType(INamedTypeSymbol type)
    {
        return type.OriginalDefinition.SpecialType ==
               SpecialType.System_Nullable_T;
    }

    private static bool IsSupportedBclDirectType(
        INamedTypeSymbol type)
    {
        return SymbolNameHelper.GetFullMetadataName(type.OriginalDefinition) is
            "System.Guid" or
            "System.DateTime" or
            "System.DateTimeOffset" or
            "System.DateOnly" or
            "System.TimeOnly" or
            "System.TimeSpan" or
            "System.Half" or
            "System.Int128" or
            "System.UInt128" or
            "System.Uri" or
            "System.Version" or
            "System.Numerics.BigInteger" or
            "System.Numerics.Complex" or
            "System.Text.Rune" or
            "System.Index" or
            "System.Range";
    }
}
