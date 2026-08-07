using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class MappingTypeEligibilityPolicy
{
    private const string ITupleMetadataName =
        "System.Runtime.CompilerServices.ITuple";

    public static bool IsEligible(
        ITypeSymbol type,
        Compilation compilation)
    {
        return CanBeUsedAsGenericArgument(type, compilation) &&
               GetUnsupportedRootReason(type, "mapping", compilation) is null;
    }

    public static string? GetUnsupportedRootReason(
        ITypeSymbol type,
        string role,
        Compilation compilation)
    {
        var typeName = type.ToDisplayString(
            SymbolDisplayFormats.FullyQualifiedNullable);

        if (!CanBeUsedAsGenericArgument(type, compilation))
        {
            return $"The {role} type '{typeName}' cannot be named in a " +
                   "generated ITypeMapper contract.";
        }

        var rootType = UnwrapNullableValueType(type);

        if (rootType is ITypeParameterSymbol)
        {
            return $"The {role} type '{typeName}' is a root type " +
                   "parameter, which Morphant does not support as a " +
                   "mapping root.";
        }

        if (rootType is IArrayTypeSymbol)
        {
            return $"The {role} type '{typeName}' is an array root. Array " +
                   "mapping requires collection support, which is not " +
                   "available.";
        }

        if (rootType is IDynamicTypeSymbol)
        {
            return null;
        }

        if (rootType is not INamedTypeSymbol namedRootType)
        {
            return $"The {role} type '{typeName}' is not a supported named " +
                   "mapping root.";
        }

        if (IsTuple(namedRootType))
        {
            return $"The {role} type '{typeName}' is a tuple root, which " +
                   "Morphant does not support.";
        }

        if (IsCollectionOrBuffer(namedRootType))
        {
            return $"The {role} type '{typeName}' is a collection or buffer " +
                   "root. Collection mapping is not available.";
        }

        if (IsDelegate(namedRootType))
        {
            return $"The {role} type '{typeName}' is a delegate root, which " +
                   "Morphant does not support.";
        }

        if (IsExpressionTree(namedRootType))
        {
            return $"The {role} type '{typeName}' is an expression-tree " +
                   "root, which Morphant does not support.";
        }

        if (IsDeferredOrAsync(namedRootType))
        {
            return $"The {role} type '{typeName}' is a deferred or async " +
                   "root, which Morphant does not support.";
        }

        return IsPushSequence(namedRootType)
            ? $"The {role} type '{typeName}' is a push-sequence root, which " +
              "Morphant does not support."
            : null;
    }

    public static bool CanBeNamed(
        ITypeSymbol type,
        Compilation compilation)
    {
        return CanBeUsedAsGenericArgument(
            type,
            compilation);
    }

    private static bool CanBeUsedAsGenericArgument(
        ITypeSymbol type,
        Compilation compilation)
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
                compilation);
        }

        if (type is not INamedTypeSymbol namedType ||
            namedType.IsAnonymousType ||
            namedType.IsFileLocal ||
            namedType.IsStatic ||
            namedType.IsUnboundGenericType ||
            !namedType.CanBeReferencedByName ||
            !compilation.IsSymbolAccessibleWithin(
                namedType,
                compilation.Assembly))
        {
            return false;
        }

        if (namedType.ContainingType is { } containingType &&
            !CanBeUsedAsGenericArgument(
                containingType,
                compilation))
        {
            return false;
        }

        return namedType.TypeArguments.All(
            typeArgument =>
                CanBeUsedAsGenericArgument(
                    typeArgument,
                    compilation));
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
