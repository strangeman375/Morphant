using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal static class GeneratedPlanNaming
{
    public const string RootNamespace = "Morphant.Generated";

    public static string BuildNamespace(
        INamedTypeSymbol destinationDefinition)
    {
        var destinationNamespace =
            destinationDefinition.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : destinationDefinition.ContainingNamespace.ToDisplayString();
        var planNamespace = string.IsNullOrEmpty(destinationNamespace)
            ? RootNamespace
            : destinationNamespace + ".Morphant.Generated";

        if (destinationDefinition.ContainingType is null)
        {
            return planNamespace;
        }

        var scopes = new Stack<string>();

        for (var containingType = destinationDefinition.ContainingType;
             containingType is not null;
             containingType = containingType.ContainingType)
        {
            var aritySuffix = containingType.Arity == 0
                ? string.Empty
                : containingType.Arity.ToString(
                    CultureInfo.InvariantCulture);

            scopes.Push(
                containingType.Name +
                aritySuffix +
                "Scope");
        }

        return planNamespace + "." + string.Join(".", scopes);
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
