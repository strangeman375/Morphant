using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal static class GeneratedPlanNaming
{
    public const string RootNamespace = "Morphant.Generated";

    public static string BuildNamespace(
        INamedTypeSymbol destinationDefinition,
        Compilation compilation)
    {
        // Eligible destinations have an unambiguous global metadata name.
        // Definition identity preserves one generic plan across substitutions.
        return BuildNamespace(
            "type:" + SymbolNameHelper.GetFullMetadataName(
                destinationDefinition.OriginalDefinition),
            compilation);
    }

    public static string BuildNamespace(
        string destinationIdentity,
        Compilation compilation)
    {
        var assembly = compilation.Assembly.Identity;
        var token = string.Concat(assembly.PublicKeyToken.Select(
            static value => value.ToString("x2", CultureInfo.InvariantCulture)));
        var identity = assembly.Name.Length.ToString(CultureInfo.InvariantCulture) +
                       ":" + assembly.Name + ":" + token + ":" +
                       destinationIdentity;

        return RootNamespace + ".N_" + HintNameHelper.GetStableHash128(identity);
    }

    public static string BuildConstructionTypeName(
        INamedTypeSymbol destinationDefinition)
    {
        return destinationDefinition.Name + "Construction";
    }

    public static string BuildConstructorParametersTypeName(
        INamedTypeSymbol destinationDefinition)
    {
        return destinationDefinition.Name + "ConstructorParameters";
    }

    public static string BuildMembersTypeName(
        INamedTypeSymbol destinationDefinition)
    {
        return destinationDefinition.Name + "Members";
    }
}
