using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class MappingTypeEligibilityPolicy
{
    private const string ITupleMetadataName =
        "System.Runtime.CompilerServices.ITuple";

    public static bool IsEligible(
        ITypeSymbol type,
        Compilation compilation,
        INamedTypeSymbol mapperType)
    {
        if (!CanBeUsedAsGenericArgument(
                type,
                compilation,
                mapperType))
        {
            return false;
        }

        var rootType = UnwrapNullableValueType(type);

        if (rootType is ITypeParameterSymbol ||
            rootType is IArrayTypeSymbol)
        {
            return false;
        }

        if (rootType is IDynamicTypeSymbol)
        {
            return true;
        }

        if (rootType is not INamedTypeSymbol namedRootType)
        {
            return false;
        }

        return !IsTuple(namedRootType) &&
               !IsCollectionOrBuffer(namedRootType) &&
               !IsDelegate(namedRootType) &&
               !IsExpressionTree(namedRootType) &&
               !IsDeferredOrAsync(namedRootType) &&
               !IsPushSequence(namedRootType);
    }

    public static bool CanBeNamed(
        ITypeSymbol type,
        Compilation compilation,
        INamedTypeSymbol mapperType)
    {
        return CanBeUsedAsGenericArgument(
            type,
            compilation,
            mapperType);
    }

    private static bool CanBeUsedAsGenericArgument(
        ITypeSymbol type,
        Compilation compilation,
        INamedTypeSymbol mapperType)
    {
        if (type.TypeKind is
                TypeKind.Error or
                TypeKind.Pointer or
                TypeKind.FunctionPointer ||
            type.SpecialType == SpecialType.System_Void ||
            type.IsRefLikeType)
        {
            return false;
        }

        if (type is IDynamicTypeSymbol or ITypeParameterSymbol)
        {
            return true;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return CanBeUsedAsGenericArgument(
                arrayType.ElementType,
                compilation,
                mapperType);
        }

        if (type is not INamedTypeSymbol namedType ||
            namedType.IsAnonymousType ||
            namedType.IsFileLocal ||
            namedType.IsStatic ||
            namedType.IsUnboundGenericType ||
            !namedType.CanBeReferencedByName ||
            !compilation.IsSymbolAccessibleWithin(
                namedType,
                mapperType))
        {
            return false;
        }

        if (namedType.ContainingType is { } containingType &&
            !CanBeUsedAsGenericArgument(
                containingType,
                compilation,
                mapperType))
        {
            return false;
        }

        return namedType.TypeArguments.All(
            typeArgument =>
                CanBeUsedAsGenericArgument(
                    typeArgument,
                    compilation,
                    mapperType));
    }

    private static ITypeSymbol UnwrapNullableValueType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.SpecialType ==
                   SpecialType.System_Nullable_T
            ? namedType.TypeArguments[0]
            : type;
    }

    private static bool IsTuple(INamedTypeSymbol type)
    {
        if (type.IsTupleType ||
            HasMetadataName(type, ITupleMetadataName) ||
            type.AllInterfaces.Any(
                static interfaceType =>
                    HasMetadataName(
                        interfaceType,
                        ITupleMetadataName)))
        {
            return true;
        }

        return type.ContainingNamespace is
                   { IsGlobalNamespace: false } containingNamespace &&
               containingNamespace.ToDisplayString() == "System" &&
               type.Name is "Tuple" or "ValueTuple";
    }

    private static bool IsCollectionOrBuffer(INamedTypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        return IsCollectionContract(type) ||
               type.AllInterfaces.Any(IsCollectionContract) ||
               GetMetadataName(type) is
                   "System.Memory`1" or
                   "System.ReadOnlyMemory`1" or
                   "System.Buffers.ReadOnlySequence`1";
    }

    private static bool IsCollectionContract(INamedTypeSymbol type)
    {
        return type.OriginalDefinition.SpecialType is
                   SpecialType.System_Collections_IEnumerable or
                   SpecialType.System_Collections_Generic_IEnumerable_T or
                   SpecialType.System_Collections_IEnumerator or
                   SpecialType.System_Collections_Generic_IEnumerator_T ||
               GetMetadataName(type) is
                   "System.Collections.Generic.IAsyncEnumerable`1" or
                   "System.Collections.Generic.IAsyncEnumerator`1";
    }

    private static bool IsDelegate(INamedTypeSymbol type)
    {
        return type.TypeKind == TypeKind.Delegate ||
               type.SpecialType is
                   SpecialType.System_Delegate or
                   SpecialType.System_MulticastDelegate;
    }

    private static bool IsExpressionTree(INamedTypeSymbol type)
    {
        return IsOrDerivesFrom(
            type,
            "System.Linq.Expressions.Expression");
    }

    private static bool IsDeferredOrAsync(INamedTypeSymbol type)
    {
        return IsOrDerivesFrom(
                   type,
                   "System.Threading.Tasks.Task") ||
               IsOrDerivesFrom(
                   type,
                   "System.Lazy`1") ||
               GetMetadataName(type) is
                   "System.Threading.Tasks.ValueTask" or
                   "System.Threading.Tasks.ValueTask`1";
    }

    private static bool IsPushSequence(INamedTypeSymbol type)
    {
        const string metadataName = "System.IObservable`1";

        return HasMetadataName(type, metadataName) ||
               type.AllInterfaces.Any(
                   static interfaceType =>
                       HasMetadataName(
                           interfaceType,
                           metadataName));
    }

    private static bool IsOrDerivesFrom(
        INamedTypeSymbol type,
        string metadataName)
    {
        for (var current = type;
             current is not null;
             current = current.BaseType)
        {
            if (HasMetadataName(current, metadataName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMetadataName(
        INamedTypeSymbol type,
        string metadataName)
    {
        return GetMetadataName(type) == metadataName;
    }

    private static string GetMetadataName(INamedTypeSymbol type)
    {
        return SymbolNameHelper.GetFullMetadataName(
            type.OriginalDefinition);
    }
}
