using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal static class GeneratedPlanNaming
{
    public const string RootNamespace = "Morphant.Generated";

    public static string BuildNamespace(
        INamedTypeSymbol destinationDefinition,
        Compilation compilation)
    {
        return RootNamespace + ".N_" +
               GeneratedEntityIdentity.ForTypeDefinition(
                   destinationDefinition,
                   compilation);
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
