using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class MappingTypeEligibilityPolicy
{
    public const string RootTypeParameterReason = "a root type parameter";

    public static bool IsEligible(
        ITypeSymbol type,
        Compilation compilation)
    {
        return GetNameability(type, compilation) ==
                   MappingTypeNameability.Available &&
               GetUnsupportedRootReason(type) is null;
    }

    public static string? GetUnsupportedRootReason(ITypeSymbol type)
    {
        var rootType = UnwrapNullableValueType(type);

        return rootType is ITypeParameterSymbol
            ? RootTypeParameterReason
            : null;
    }

    internal static bool IsDeferredOpaqueRoot(ITypeSymbol type)
    {
        var rootType = UnwrapNullableValueType(type);

        if (rootType is IArrayTypeSymbol)
        {
            return true;
        }

        return rootType is INamedTypeSymbol namedRootType &&
               (IsCollectionOrBuffer(namedRootType) ||
                IsDelegate(namedRootType) ||
                IsExpressionTree(namedRootType) ||
                IsDeferredOrAsync(namedRootType) ||
                IsPushSequence(namedRootType));
    }

    public static bool CanBeNamed(
        ITypeSymbol type,
        Compilation compilation)
    {
        return GetNameability(type, compilation) ==
               MappingTypeNameability.Available;
    }

    internal static MappingTypeNameability GetNameability(
        ITypeSymbol type,
        Compilation compilation)
    {
        return GetNameability(
            type,
            compilation,
            inspectTypeParameterConstraints: false,
            new HashSet<ITypeParameterSymbol>(
                TypeParameterComparer.Instance));
    }

    internal static bool CanCopyTypeParameterConstraints(
        INamedTypeSymbol type,
        Compilation compilation)
    {
        var visited = new HashSet<ITypeParameterSymbol>(
            TypeParameterComparer.Instance);

        for (var current = type;
             current is not null;
             current = current.ContainingType)
        {
            foreach (var typeParameter in current.TypeParameters)
            {
                foreach (var constraintType in
                         typeParameter.ConstraintTypes)
                {
                    if (GetNameability(
                            constraintType,
                            compilation,
                            inspectTypeParameterConstraints: true,
                            visited) != MappingTypeNameability.Available)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static MappingTypeNameability GetNameability(
        ITypeSymbol type,
        Compilation compilation,
        bool inspectTypeParameterConstraints,
        HashSet<ITypeParameterSymbol> visitedTypeParameters)
    {
        if (type.TypeKind is
                TypeKind.Error or
                TypeKind.Pointer or
                TypeKind.FunctionPointer ||
            type.SpecialType == SpecialType.System_Void ||
            type.IsRefLikeType)
        {
            return MappingTypeNameability.CompilerOwned;
        }

        if (type is IDynamicTypeSymbol)
        {
            return MappingTypeNameability.Available;
        }

        if (type is ITypeParameterSymbol typeParameter)
        {
            if (!inspectTypeParameterConstraints ||
                !visitedTypeParameters.Add(typeParameter))
            {
                return MappingTypeNameability.Available;
            }

            foreach (var constraintType in typeParameter.ConstraintTypes)
            {
                var constraintNameability = GetNameability(
                    constraintType,
                    compilation,
                    inspectTypeParameterConstraints: true,
                    visitedTypeParameters);

                if (constraintNameability !=
                    MappingTypeNameability.Available)
                {
                    return constraintNameability;
                }
            }

            return MappingTypeNameability.Available;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return GetNameability(
                arrayType.ElementType,
                compilation,
                inspectTypeParameterConstraints: true,
                visitedTypeParameters);
        }

        if (type is not INamedTypeSymbol namedType ||
            namedType.IsAnonymousType ||
            namedType.IsStatic ||
            namedType.IsUnboundGenericType ||
            !namedType.CanBeReferencedByName)
        {
            return MappingTypeNameability.CompilerOwned;
        }

        var result = namedType.IsFileLocal ||
                     !compilation.IsSymbolAccessibleWithin(
                         namedType,
                         compilation.Assembly) ||
                     !IsAvailableThroughGlobalAlias(
                         namedType.ContainingAssembly,
                         compilation)
            ? MappingTypeNameability.Unavailable
            : MappingTypeNameability.Available;

        if (namedType.ContainingType is { } containingType)
        {
            var containingNameability = GetNameability(
                containingType,
                compilation,
                inspectTypeParameterConstraints: true,
                visitedTypeParameters);

            if (containingNameability != MappingTypeNameability.Available)
            {
                if (containingNameability ==
                    MappingTypeNameability.CompilerOwned)
                {
                    return MappingTypeNameability.CompilerOwned;
                }

                result = MappingTypeNameability.Unavailable;
            }
        }

        for (var index = 0;
             index < namedType.TypeArguments.Length;
             index++)
        {
            var typeArgument = namedType.TypeArguments[index];
            var argumentNameability = GetNameability(
                typeArgument,
                compilation,
                inspectTypeParameterConstraints: true,
                visitedTypeParameters);

            if (argumentNameability == MappingTypeNameability.CompilerOwned)
            {
                return MappingTypeNameability.CompilerOwned;
            }

            if (argumentNameability == MappingTypeNameability.Unavailable)
            {
                result = MappingTypeNameability.Unavailable;
            }

            if (typeArgument is not ITypeParameterSymbol)
            {
                continue;
            }

            // Pair-specific methods reproduce definition constraints for a
            // type parameter used directly as a generic argument.
            foreach (var constraintType in
                     namedType.TypeParameters[index].ConstraintTypes)
            {
                var constraintNameability = GetNameability(
                    constraintType,
                    compilation,
                    inspectTypeParameterConstraints: true,
                    visitedTypeParameters);

                if (constraintNameability ==
                    MappingTypeNameability.CompilerOwned)
                {
                    return MappingTypeNameability.CompilerOwned;
                }

                if (constraintNameability ==
                    MappingTypeNameability.Unavailable)
                {
                    result = MappingTypeNameability.Unavailable;
                }
            }
        }

        return result;
    }

    private static bool IsAvailableThroughGlobalAlias(
        IAssemblySymbol assembly,
        Compilation compilation)
    {
        if (SymbolEqualityComparer.Default.Equals(
                assembly,
                compilation.Assembly))
        {
            return true;
        }

        var reference = compilation.GetMetadataReference(assembly);
        var aliases = reference?.Properties.Aliases;

        return aliases is { } values &&
               (values.IsDefaultOrEmpty ||
                values.Contains("global", StringComparer.Ordinal));
    }

    private static ITypeSymbol UnwrapNullableValueType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.SpecialType ==
                   SpecialType.System_Nullable_T
            ? namedType.TypeArguments[0]
            : type;
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

    private sealed class TypeParameterComparer :
        IEqualityComparer<ITypeParameterSymbol>
    {
        public static TypeParameterComparer Instance { get; } = new();

        public bool Equals(
            ITypeParameterSymbol? left,
            ITypeParameterSymbol? right)
        {
            return SymbolEqualityComparer.Default.Equals(left, right);
        }

        public int GetHashCode(ITypeParameterSymbol typeParameter)
        {
            return SymbolEqualityComparer.Default.GetHashCode(typeParameter);
        }
    }
}

internal enum MappingTypeNameability
{
    Available,
    Unavailable,
    CompilerOwned
}
