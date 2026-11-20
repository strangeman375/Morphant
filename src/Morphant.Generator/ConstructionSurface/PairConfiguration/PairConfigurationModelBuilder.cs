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
        var typeParameters = GeneratedTypeNameBuilder.CollectTypeParameters(
            pair.SourceType, pair.DestinationType);
        var typeParameterNames =
            GeneratedTypeNameBuilder.AllocateTypeParameterNames(
                typeParameters);
        var sourceType = pair.SourceType;
        var destinationType = pair.DestinationType;
        var declarativeSourceType =
            MappingTypeNormalization.NormalizeDeclarativeSource(
                sourceType,
                compilation,
                normalizeDynamic: false);
        var manualSourceType =
            MappingTypeNormalization.NormalizeManualSource(
                sourceType,
                compilation,
                normalizeDynamic: false);
        var previousDestinationType =
            MappingTypeNormalization.NormalizePreviousDestination(
                destinationType,
                compilation,
                normalizeDynamic: false);
        var sourceTypeName = GeneratedTypeNameBuilder.Build(
            sourceType,
            typeParameterNames,
            normalizeDynamic: false);
        var destinationTypeName = GeneratedTypeNameBuilder.Build(
            destinationType,
            typeParameterNames,
            normalizeDynamic: false);
        var builderTypeName =
            $"global::Morphant.MappingBuilder<{sourceTypeName}, {destinationTypeName}>";
        var tupleShape = BclTupleShapePolicy.TryCreate(
            previousDestinationType);
        var typeParameterModels = PairTypeParameterModelBuilder.Build(
            sourceType,
            destinationType,
            typeParameters,
            typeParameterNames,
            compilation);

        return new PairConfigurationModel(
            GeneratedMappingExtensionNaming.CommonContainerTypeName,
            builderTypeName,
            GeneratedTypeNameBuilder.Build(
                declarativeSourceType,
                typeParameterNames,
                normalizeDynamic: false),
            GeneratedTypeNameBuilder.Build(
                manualSourceType,
                typeParameterNames,
                normalizeDynamic: false),
            GeneratedTypeNameBuilder.Build(
                MappingTypeNormalization.NormalizeManualSource(destinationType, compilation, normalizeDynamic: false),
                typeParameterNames,
                normalizeDynamic: false),
            GeneratedTypeNameBuilder.Build(
                previousDestinationType,
                typeParameterNames,
                normalizeDynamic: false),
            pair.Capabilities.StructuredConstruction,
            pair.Capabilities.StructuredConstruction
                ? tupleShape is { } constructionTuple
                    ? BclTuplePlanNaming.BuildPlanTypeReference(
                        constructionTuple,
                        BclTuplePlanNaming.BuildConstructionTypeName(
                            constructionTuple),
                        compilation,
                        typeParameterNames)
                    : BuildPlanTypeName(
                        (INamedTypeSymbol)previousDestinationType,
                        typeParameterNames,
                        GeneratedPlanNaming.BuildConstructionTypeName,
                        compilation)
                : destinationTypeName,
            pair.Capabilities.Members
                ? tupleShape is { } membersTuple
                    ? BclTuplePlanNaming.BuildPlanTypeReference(
                        membersTuple,
                        BclTuplePlanNaming.BuildMembersTypeName(
                            membersTuple),
                        compilation,
                        typeParameterNames)
                    : BuildPlanTypeName(
                        (INamedTypeSymbol)previousDestinationType,
                        typeParameterNames,
                        GeneratedPlanNaming.BuildMembersTypeName,
                        compilation)
                : null,
            typeParameterModels)
        {
            ObsoleteWarnings = ObsoleteTypeWarnings.Collect(
                sourceType, destinationType)
        };
    }

    private static string BuildPlanTypeName(
        INamedTypeSymbol destinationType,
        IReadOnlyDictionary<ITypeParameterSymbol, string> typeParameterNames,
        Func<INamedTypeSymbol, string> buildTypeName,
        Compilation compilation)
    {
        var definition = destinationType.OriginalDefinition;
        var planNamespace =
            GeneratedPlanNaming.BuildNamespace(definition, compilation);
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
                                typeParameterNames,
                                normalizeDynamic: false))) +
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
