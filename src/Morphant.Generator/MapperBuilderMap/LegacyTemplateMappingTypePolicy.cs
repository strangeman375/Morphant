using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MapperBuilderMap;

// Transitional eligibility policy for the superseded Template() surface.
// New pipelines consume MappingPairModel instead.
internal static class LegacyTemplateMappingTypePolicy
{
    public static bool IsSupported(ITypeSymbol type)
    {
        return IsSupportedRoot(type) &&
               IsRepresentable(type);
    }

    private static bool IsSupportedRoot(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
        {
            return false;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return true;
        }

        namedType = UnwrapNullableValueType(namedType);

        return !IsTuple(namedType) &&
               !IsDelegate(namedType) &&
               !IsCollection(namedType);
    }

    private static bool IsRepresentable(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Error)
        {
            return false;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return IsRepresentable(arrayType.ElementType);
        }

        if (type is IPointerTypeSymbol or
            IFunctionPointerTypeSymbol)
        {
            return false;
        }

        if (type is INamedTypeSymbol namedType)
        {
            if (namedType.IsFileLocal ||
                namedType.ContainingType is { } containingType &&
                !IsRepresentable(containingType))
            {
                return false;
            }

            return namedType.TypeArguments.All(IsRepresentable);
        }

        return type is ITypeParameterSymbol or
            IDynamicTypeSymbol;
    }

    private static INamedTypeSymbol UnwrapNullableValueType(
        INamedTypeSymbol type)
    {
        return type.OriginalDefinition.SpecialType ==
                   SpecialType.System_Nullable_T &&
               type.TypeArguments[0] is INamedTypeSymbol underlyingType
            ? underlyingType
            : type;
    }

    private static bool IsTuple(INamedTypeSymbol type)
    {
        if (type.IsTupleType)
        {
            return true;
        }

        return type.ContainingNamespace is
                   { IsGlobalNamespace: false } containingNamespace &&
               containingNamespace.ToDisplayString() == "System" &&
               type.Name is "Tuple" or "ValueTuple";
    }

    private static bool IsDelegate(INamedTypeSymbol type)
    {
        return type.TypeKind == TypeKind.Delegate ||
               type.SpecialType is
                   SpecialType.System_Delegate or
                   SpecialType.System_MulticastDelegate;
    }

    private static bool IsCollection(INamedTypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        return IsEnumerable(type) ||
               type.AllInterfaces.Any(IsEnumerable);
    }

    private static bool IsEnumerable(INamedTypeSymbol type)
    {
        return type.OriginalDefinition.SpecialType is
            SpecialType.System_Collections_IEnumerable or
            SpecialType.System_Collections_Generic_IEnumerable_T;
    }
}
