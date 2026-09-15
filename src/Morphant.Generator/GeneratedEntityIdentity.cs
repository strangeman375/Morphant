using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal static class GeneratedEntityIdentity
{
    public static string ForTypeDefinition(
        INamedTypeSymbol type,
        Compilation compilation)
    {
        // Supported types are unambiguous through their global metadata name.
        // Generic definitions share one identity across closed substitutions.
        return Create(
            "type:" + SymbolNameHelper.GetFullMetadataName(
                type.OriginalDefinition),
            compilation);
    }

    public static string Create(string entityIdentity, Compilation compilation)
    {
        var assembly = compilation.Assembly.Identity;
        var token = string.Concat(assembly.PublicKeyToken.Select(
            static value => value.ToString("x2", CultureInfo.InvariantCulture)));
        var identity = assembly.Name.Length.ToString(CultureInfo.InvariantCulture) +
                       ":" + assembly.Name + ":" + token + ":" + entityIdentity;

        return HintNameHelper.GetStableHash128(identity);
    }

    public static string Combine(params string[] parts)
    {
        return string.Concat(parts.Select(static part =>
            part.Length.ToString(CultureInfo.InvariantCulture) + ":" + part));
    }
}
