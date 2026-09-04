using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal static class GeneratedPlanNaming
{
    public const string RootNamespace = "Morphant.Generated";

    private const string TypePlansRootNamespace =
        RootNamespace + ".Types";

    public static string BuildNamespace(
        INamedTypeSymbol destinationDefinition)
    {
        var namespaceScopes = new Stack<string>();

        for (var containingNamespace =
                 destinationDefinition.ContainingNamespace;
             !containingNamespace.IsGlobalNamespace;
             containingNamespace = containingNamespace.ContainingNamespace)
        {
            namespaceScopes.Push(
                "N_" + EscapeScopeName(containingNamespace.Name));
        }

        var scopes = new List<string>(namespaceScopes.Count);

        while (namespaceScopes.Count > 0)
        {
            scopes.Add(namespaceScopes.Pop());
        }

        var containingTypeScopes = new Stack<string>();

        for (var containingType = destinationDefinition.ContainingType;
             containingType is not null;
             containingType = containingType.ContainingType)
        {
            var aritySuffix = containingType.Arity == 0
                ? string.Empty
                : "_A" + containingType.Arity.ToString(
                    CultureInfo.InvariantCulture);

            containingTypeScopes.Push(
                "T_" +
                EscapeScopeName(containingType.Name) +
                aritySuffix);
        }

        while (containingTypeScopes.Count > 0)
        {
            scopes.Add(containingTypeScopes.Pop());
        }

        return TypePlansRootNamespace +
               (scopes.Count == 0
                   ? string.Empty
                   : "." + string.Join(".", scopes)) +
               ".Plans";
    }

    private static string EscapeScopeName(string name)
    {
        // A single underscore introduces generated scope metadata. Doubling
        // user underscores keeps the encoding injective.
        return name.Replace("_", "__");
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
