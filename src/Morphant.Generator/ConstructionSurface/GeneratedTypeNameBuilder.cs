using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.ConstructionSurface;

internal static class GeneratedTypeNameBuilder
{
    public static string Build(
        ITypeSymbol type,
        IReadOnlyDictionary<ITypeParameterSymbol, string>? typeParameterNames =
            null,
        bool escapeTypeParameterNames = true,
        bool normalizeDynamic = true)
    {
        var parts = type.ToDisplayParts(
            SymbolDisplayFormats.FullyQualifiedNullable);

        return string.Concat(parts.Select(part =>
            part.Symbol is IDynamicTypeSymbol && normalizeDynamic
                ? "object"
                : part.Symbol is ITypeParameterSymbol typeParameter &&
                  typeParameterNames is not null &&
                  TryGetName(
                      typeParameterNames,
                      typeParameter,
                      out var name)
                ? escapeTypeParameterNames
                    ? EscapeIdentifier(name)
                    : name
                : part.ToString()));
    }

    public static ImmutableArray<ITypeParameterSymbol> CollectTypeParameters(
        params ITypeSymbol[] types)
    {
        var result = ImmutableArray.CreateBuilder<ITypeParameterSymbol>();
        var seen = new HashSet<ITypeParameterSymbol>(
            TypeParameterSymbolComparer.Instance);

        foreach (var type in types)
        {
            AddTypeParameters(type, result, seen);
        }

        return result.ToImmutable();
    }

    public static IReadOnlyDictionary<ITypeParameterSymbol, string>
        AllocateTypeParameterNames(
            IReadOnlyList<ITypeParameterSymbol> typeParameters)
    {
        var result = new Dictionary<ITypeParameterSymbol, string>(
            TypeParameterSymbolComparer.Instance);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var typeParameter in typeParameters)
        {
            var preferredName = typeParameter.Name;
            var name = preferredName;
            var suffix = 2;

            while (!usedNames.Add(name))
            {
                name = preferredName + suffix;
                suffix++;
            }

            result.Add(typeParameter, name);
        }

        return result;
    }

    private static void AddTypeParameters(
        ITypeSymbol type,
        ImmutableArray<ITypeParameterSymbol>.Builder result,
        HashSet<ITypeParameterSymbol> seen)
    {
        if (type is ITypeParameterSymbol typeParameter)
        {
            if (seen.Add(typeParameter))
            {
                result.Add(typeParameter);
            }

            return;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            AddTypeParameters(arrayType.ElementType, result, seen);
            return;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return;
        }

        if (namedType.ContainingType is { } containingType)
        {
            AddTypeParameters(containingType, result, seen);
        }

        foreach (var typeArgument in namedType.TypeArguments)
        {
            AddTypeParameters(typeArgument, result, seen);
        }
    }

    private static bool TryGetName(
        IReadOnlyDictionary<ITypeParameterSymbol, string> names,
        ITypeParameterSymbol typeParameter,
        out string name)
    {
        foreach (var pair in names)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    pair.Key,
                    typeParameter))
            {
                name = pair.Value;
                return true;
            }
        }

        name = string.Empty;
        return false;
    }

    private static string EscapeIdentifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value
            : value;
    }

    private sealed class TypeParameterSymbolComparer :
        IEqualityComparer<ITypeParameterSymbol>
    {
        public static TypeParameterSymbolComparer Instance { get; } = new();

        public bool Equals(
            ITypeParameterSymbol? x,
            ITypeParameterSymbol? y)
        {
            return SymbolEqualityComparer.Default.Equals(x, y);
        }

        public int GetHashCode(ITypeParameterSymbol obj)
        {
            return SymbolEqualityComparer.Default.GetHashCode(obj);
        }
    }
}
