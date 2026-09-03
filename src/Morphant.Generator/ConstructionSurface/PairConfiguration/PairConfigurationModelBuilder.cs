using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Morphant.Generator.ConstructionSurface.ConstructionPlan;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.ConstructionSurface.PairConfiguration;

internal static class PairConfigurationModelBuilder
{
    public static PairConfigurationModel Build(
        MappingPairModel pair,
        MappingSurfaceModel surface,
        Compilation compilation)
    {
        var typeParameters = CollectTypeParameters(pair, surface);
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
        var mapperTypeName = surface.Kind == MappingSurfaceKind.Shared
            ? "TMapper"
            : GeneratedTypeNameBuilder.Build(
                surface.MapperSelfType,
                typeParameterNames,
                normalizeDynamic: false);
        var builderTypeName =
            "global::Morphant.MappingBuilder<" +
            mapperTypeName +
            ", " +
            sourceTypeName +
            ", " +
            destinationTypeName +
            ">";
        var receiverTypeName = surface.Kind == MappingSurfaceKind.Shared
            ? "global::Morphant.MapperBuilderBase<" +
              builderTypeName +
              ">"
            : builderTypeName;
        var tupleShape = BclTupleShapePolicy.TryCreate(
            previousDestinationType);
        var typeParameterModels = PairTypeParameterModelBuilder.Build(
            sourceType,
            destinationType,
            typeParameters,
            typeParameterNames,
            compilation,
            includeDeclarationConstraints:
                surface.Kind != MappingSurfaceKind.Shared);

        if (surface.Kind == MappingSurfaceKind.Shared)
        {
            typeParameterModels = typeParameterModels.Insert(
                0,
                new ConstructionTypeParameterModel(
                    "TMapper",
                    ImmutableArray.Create(
                        "global::Morphant.TypeMapper<TMapper>"),
                    RequiresNullableAnnotationsDisabled: false));
        }

        return new PairConfigurationModel(
            builderTypeName,
            receiverTypeName,
            GeneratedTypeNameBuilder.Build(
                declarativeSourceType,
                typeParameterNames,
                normalizeDynamic: false),
            GeneratedTypeNameBuilder.Build(
                manualSourceType,
                typeParameterNames,
                normalizeDynamic: false),
            destinationTypeName,
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
                        typeParameterNames)
                    : BuildPlanTypeName(
                        (INamedTypeSymbol)previousDestinationType,
                        typeParameterNames,
                        GeneratedPlanNaming.BuildConstructionTypeName)
                : destinationTypeName,
            pair.Capabilities.Members
                ? tupleShape is { } membersTuple
                    ? BclTuplePlanNaming.BuildPlanTypeReference(
                        membersTuple,
                        BclTuplePlanNaming.BuildMembersTypeName(
                            membersTuple),
                        typeParameterNames)
                    : BuildPlanTypeName(
                        (INamedTypeSymbol)previousDestinationType,
                        typeParameterNames,
                        GeneratedPlanNaming.BuildMembersTypeName)
                : null,
            typeParameterModels);
    }

    private static ImmutableArray<ITypeParameterSymbol> CollectTypeParameters(
        MappingPairModel pair,
        MappingSurfaceModel surface)
    {
        return surface.Kind == MappingSurfaceKind.Shared
            ? GeneratedTypeNameBuilder.CollectTypeParameters(
                pair.SourceType,
                pair.DestinationType)
            : GeneratedTypeNameBuilder.CollectTypeParameters(
                surface.DeclaringMapperType,
                surface.MapperSelfType,
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
