using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.ConstructionSurface.PairConfiguration;

internal static class PairConfigurationModelBuilder
{
    public static PairConfigurationModel Build(
        MappingPairModel pair,
        Compilation compilation)
    {
        var typeParameters = CollectTypeParameters(pair);
        var typeParameterNames =
            GeneratedTypeNameBuilder.AllocateTypeParameterNames(
                typeParameters);
        var sourceType = MappingTypeNormalization.NormalizeDynamic(
            pair.SourceType,
            compilation);
        var destinationType = MappingTypeNormalization.NormalizeDynamic(
            pair.DestinationType,
            compilation);
        var declarativeSourceType =
            MappingTypeNormalization.NormalizeDeclarativeSource(
                sourceType,
                compilation);
        var manualSourceType =
            MappingTypeNormalization.NormalizeManualSource(
                sourceType,
                compilation);
        var previousDestinationType =
            MappingTypeNormalization.NormalizePreviousDestination(
                destinationType,
                compilation);
        var sourceTypeName = GeneratedTypeNameBuilder.Build(
            sourceType,
            typeParameterNames);
        var destinationTypeName = GeneratedTypeNameBuilder.Build(
            destinationType,
            typeParameterNames);
        var builderTypeName =
            "global::Morphant.MapperBuilder<" +
            sourceTypeName +
            ", " +
            destinationTypeName +
            ">";

        return new PairConfigurationModel(
            builderTypeName,
            GeneratedTypeNameBuilder.Build(
                declarativeSourceType,
                typeParameterNames),
            GeneratedTypeNameBuilder.Build(
                manualSourceType,
                typeParameterNames),
            destinationTypeName,
            GeneratedTypeNameBuilder.Build(
                previousDestinationType,
                typeParameterNames),
            pair.Capabilities.StructuredConstruction
                ? BuildPlanTypeName(
                    (INamedTypeSymbol)previousDestinationType,
                    typeParameterNames,
                    GeneratedPlanNaming.BuildConstructionTypeName)
                : destinationTypeName,
            pair.Capabilities.Members
                ? BuildPlanTypeName(
                    (INamedTypeSymbol)previousDestinationType,
                    typeParameterNames,
                    GeneratedPlanNaming.BuildMembersTypeName)
                : null,
            PairTypeParameterModelBuilder.Build(
                sourceType,
                destinationType,
                typeParameters,
                typeParameterNames,
                compilation));
    }

    private static ImmutableArray<ITypeParameterSymbol> CollectTypeParameters(
        MappingPairModel pair)
    {
        return GeneratedTypeNameBuilder.CollectTypeParameters(
            pair.SourceType,
            pair.DestinationType);
    }

    private static string BuildPlanTypeName(
        INamedTypeSymbol destinationType,
        IReadOnlyDictionary<ITypeParameterSymbol, string> typeParameterNames,
        Func<INamedTypeSymbol, string> buildTypeName)
    {
        var definition = destinationType.OriginalDefinition;
        var planNamespace =
            GeneratedPlanNaming.BuildNamespace(definition);
        var planTypeName = buildTypeName(definition);
        var arguments = CollectTypeArguments(destinationType);

        return "global::" +
               planNamespace +
               "." +
               planTypeName +
               (arguments.IsEmpty
                   ? string.Empty
                   : "<" +
                     string.Join(
                         ", ",
                         arguments.Select(argument =>
                             GeneratedTypeNameBuilder.Build(
                                 argument,
                                 typeParameterNames))) +
                     ">");
    }

    private static ImmutableArray<ITypeSymbol> CollectTypeArguments(
        INamedTypeSymbol destinationType)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();

        for (var current = destinationType;
             current is not null;
             current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        var result = ImmutableArray.CreateBuilder<ITypeSymbol>();

        while (containingTypes.Count > 0)
        {
            result.AddRange(containingTypes.Pop().TypeArguments);
        }

        return result.ToImmutable();
    }
}
