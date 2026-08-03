using System.Text;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class MappingTypeIdentityPolicy
{
    public static MappingTypeIdentity Create(ITypeSymbol type)
    {
        var key = new StringBuilder();
        var displayName = new StringBuilder();

        Append(type, key, displayName);

        return new MappingTypeIdentity(
            key.ToString(),
            displayName.ToString());
    }

    public static bool AreEquivalent(
        ITypeSymbol left,
        ITypeSymbol right)
    {
        var leftTypeParameter = left as ITypeParameterSymbol;
        var rightTypeParameter = right as ITypeParameterSymbol;

        if (leftTypeParameter is not null ||
            rightTypeParameter is not null)
        {
            return leftTypeParameter is not null &&
                   rightTypeParameter is not null &&
                   SymbolEqualityComparer.Default.Equals(
                       leftTypeParameter,
                       rightTypeParameter);
        }

        if (IsObjectLike(left) || IsObjectLike(right))
        {
            return IsObjectLike(left) &&
                   IsObjectLike(right);
        }

        var leftArray = left as IArrayTypeSymbol;
        var rightArray = right as IArrayTypeSymbol;

        if (leftArray is not null || rightArray is not null)
        {
            return leftArray is not null &&
                   rightArray is not null &&
                   leftArray.Rank == rightArray.Rank &&
                   leftArray.IsSZArray == rightArray.IsSZArray &&
                   AreEquivalent(
                       leftArray.ElementType,
                       rightArray.ElementType);
        }

        if (left is not INamedTypeSymbol leftNamed ||
            right is not INamedTypeSymbol rightNamed)
        {
            return false;
        }

        leftNamed = Normalize(leftNamed);
        rightNamed = Normalize(rightNamed);

        if (!SymbolEqualityComparer.Default.Equals(
                leftNamed.OriginalDefinition,
                rightNamed.OriginalDefinition) ||
            !AreContainingTypesEquivalent(
                leftNamed.ContainingType,
                rightNamed.ContainingType) ||
            leftNamed.TypeArguments.Length !=
                rightNamed.TypeArguments.Length)
        {
            return false;
        }

        for (var index = 0;
             index < leftNamed.TypeArguments.Length;
             index++)
        {
            if (!AreEquivalent(
                    leftNamed.TypeArguments[index],
                    rightNamed.TypeArguments[index]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool CanPairsUnify(
        ITypeSymbol leftSource,
        ITypeSymbol leftDestination,
        ITypeSymbol rightSource,
        ITypeSymbol rightDestination)
    {
        var substitutions =
            new Dictionary<ITypeParameterSymbol, ITypeSymbol>(
                TypeParameterComparer.Instance);

        return TryUnify(
                   leftSource,
                   rightSource,
                   substitutions) &&
               TryUnify(
                   leftDestination,
                   rightDestination,
                   substitutions);
    }

    private static void Append(
        ITypeSymbol type,
        StringBuilder key,
        StringBuilder displayName)
    {
        if (IsObjectLike(type))
        {
            key.Append("special:System.Object");
            displayName.Append("global::System.Object");
            return;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            key.Append("array(");
            Append(arrayType.ElementType, key, displayName);
            key.Append(';').Append(arrayType.Rank).Append(')');

            displayName.Append('[');

            if (!arrayType.IsSZArray)
            {
                displayName.Append(',', arrayType.Rank - 1);
            }

            displayName.Append(']');
            return;
        }

        if (type is ITypeParameterSymbol typeParameter)
        {
            key.Append("parameter:")
                .Append(GetContainingSymbolIdentity(
                    typeParameter.ContainingSymbol))
                .Append(':')
                .Append((int)typeParameter.TypeParameterKind)
                .Append(':')
                .Append(typeParameter.Ordinal);
            displayName.Append(typeParameter.Name);
            return;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            key.Append(type.Kind)
                .Append(':')
                .Append(type.ToDisplayString());
            displayName.Append(type.ToDisplayString());
            return;
        }

        AppendNamed(
            Normalize(namedType),
            key,
            displayName);
    }

    private static void AppendNamed(
        INamedTypeSymbol type,
        StringBuilder key,
        StringBuilder displayName)
    {
        if (type.ContainingType is { } containingType)
        {
            AppendNamed(
                Normalize(containingType),
                key,
                displayName);
            key.Append('+');
            displayName.Append('.');
        }
        else
        {
            key.Append("named:[")
                .Append(type.ContainingAssembly.Identity)
                .Append("]:");
            displayName.Append("global::");

            if (!type.ContainingNamespace.IsGlobalNamespace)
            {
                var namespaceName =
                    type.ContainingNamespace.ToDisplayString();

                key.Append(namespaceName).Append('.');
                displayName.Append(namespaceName).Append('.');
            }
        }

        key.Append(type.MetadataName);
        displayName.Append(type.Name);

        if (type.TypeArguments.IsDefaultOrEmpty)
        {
            return;
        }

        key.Append('<');
        displayName.Append('<');

        for (var index = 0;
             index < type.TypeArguments.Length;
             index++)
        {
            if (index > 0)
            {
                key.Append(',');
                displayName.Append(", ");
            }

            Append(
                type.TypeArguments[index],
                key,
                displayName);
        }

        key.Append('>');
        displayName.Append('>');
    }

    private static string GetContainingSymbolIdentity(ISymbol symbol)
    {
        if (symbol is INamedTypeSymbol type)
        {
            return type.ContainingAssembly.Identity + ":" +
                   SymbolNameHelper.GetFullMetadataName(type);
        }

        return symbol.GetDocumentationCommentId() ??
               symbol.ToDisplayString(
                   SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static bool AreContainingTypesEquivalent(
        INamedTypeSymbol? left,
        INamedTypeSymbol? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return AreEquivalent(left, right);
    }

    private static bool TryUnify(
        ITypeSymbol left,
        ITypeSymbol right,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> substitutions)
    {
        left = Resolve(left, substitutions);
        right = Resolve(right, substitutions);

        if (left is ITypeParameterSymbol leftTypeParameter)
        {
            return TryBind(
                leftTypeParameter,
                right,
                substitutions);
        }

        if (right is ITypeParameterSymbol rightTypeParameter)
        {
            return TryBind(
                rightTypeParameter,
                left,
                substitutions);
        }

        if (IsObjectLike(left) || IsObjectLike(right))
        {
            return IsObjectLike(left) && IsObjectLike(right);
        }

        var leftArray = left as IArrayTypeSymbol;
        var rightArray = right as IArrayTypeSymbol;

        if (leftArray is not null || rightArray is not null)
        {
            return leftArray is not null &&
                   rightArray is not null &&
                   leftArray.Rank == rightArray.Rank &&
                   leftArray.IsSZArray == rightArray.IsSZArray &&
                   TryUnify(
                       leftArray.ElementType,
                       rightArray.ElementType,
                       substitutions);
        }

        if (left is not INamedTypeSymbol leftNamed ||
            right is not INamedTypeSymbol rightNamed)
        {
            return false;
        }

        leftNamed = Normalize(leftNamed);
        rightNamed = Normalize(rightNamed);

        if (!SymbolEqualityComparer.Default.Equals(
                leftNamed.OriginalDefinition,
                rightNamed.OriginalDefinition) ||
            leftNamed.TypeArguments.Length !=
                rightNamed.TypeArguments.Length ||
            !TryUnifyContainingTypes(
                leftNamed.ContainingType,
                rightNamed.ContainingType,
                substitutions))
        {
            return false;
        }

        for (var index = 0;
             index < leftNamed.TypeArguments.Length;
             index++)
        {
            if (!TryUnify(
                    leftNamed.TypeArguments[index],
                    rightNamed.TypeArguments[index],
                    substitutions))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryBind(
        ITypeParameterSymbol typeParameter,
        ITypeSymbol type,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> substitutions)
    {
        type = Resolve(type, substitutions);

        if (type is ITypeParameterSymbol otherTypeParameter &&
            SymbolEqualityComparer.Default.Equals(
                typeParameter,
                otherTypeParameter))
        {
            return true;
        }

        if (Contains(type, typeParameter, substitutions))
        {
            return false;
        }

        substitutions.Add(typeParameter, type);
        return true;
    }

    private static ITypeSymbol Resolve(
        ITypeSymbol type,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol>
            substitutions)
    {
        while (type is ITypeParameterSymbol typeParameter &&
               substitutions.TryGetValue(
                   typeParameter,
                   out var replacement))
        {
            type = replacement;
        }

        return type;
    }

    private static bool Contains(
        ITypeSymbol type,
        ITypeParameterSymbol searchedTypeParameter,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol>
            substitutions)
    {
        type = Resolve(type, substitutions);

        if (type is ITypeParameterSymbol typeParameter)
        {
            return SymbolEqualityComparer.Default.Equals(
                typeParameter,
                searchedTypeParameter);
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return Contains(
                arrayType.ElementType,
                searchedTypeParameter,
                substitutions);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        namedType = Normalize(namedType);

        if (namedType.ContainingType is { } containingType &&
            Contains(
                containingType,
                searchedTypeParameter,
                substitutions))
        {
            return true;
        }

        return namedType.TypeArguments.Any(
            typeArgument =>
                Contains(
                    typeArgument,
                    searchedTypeParameter,
                    substitutions));
    }

    private static bool TryUnifyContainingTypes(
        INamedTypeSymbol? left,
        INamedTypeSymbol? right,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> substitutions)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return TryUnify(left, right, substitutions);
    }

    private static bool IsObjectLike(ITypeSymbol type)
    {
        return type is IDynamicTypeSymbol ||
               type.SpecialType == SpecialType.System_Object;
    }

    private static INamedTypeSymbol Normalize(INamedTypeSymbol type)
    {
        if (type.IsTupleType &&
            type.TupleUnderlyingType is { } tupleUnderlyingType)
        {
            type = tupleUnderlyingType;
        }

        if (type.IsNativeIntegerType &&
            type.NativeIntegerUnderlyingType is
                { } nativeIntegerUnderlyingType)
        {
            type = nativeIntegerUnderlyingType;
        }

        return type;
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
