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
        var sourceType = NormalizeDynamic(
            pair.SourceType,
            compilation);
        var destinationType = NormalizeDynamic(
            pair.DestinationType,
            compilation);
        var declarativeSourceType =
            NormalizeDeclarativeSource(sourceType);
        var manualSourceType = NormalizeManualSource(sourceType);
        var previousDestinationType =
            NormalizePreviousDestination(destinationType);
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
                ? BuildConstructionPlanTypeName(
                    (INamedTypeSymbol)previousDestinationType,
                    typeParameterNames)
                : destinationTypeName,
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

    private static ITypeSymbol NormalizeDynamic(
        ITypeSymbol type,
        Compilation compilation)
    {
        return type is IDynamicTypeSymbol
            ? compilation.GetSpecialType(SpecialType.System_Object)
            : type;
    }

    private static ITypeSymbol NormalizeDeclarativeSource(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType ==
            SpecialType.System_Nullable_T)
        {
            return namedType.TypeArguments[0];
        }

        return type.IsReferenceType
            ? type.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
            : type;
    }

    private static ITypeSymbol NormalizeManualSource(ITypeSymbol type)
    {
        return type.IsReferenceType
            ? type.WithNullableAnnotation(NullableAnnotation.Annotated)
            : type;
    }

    private static ITypeSymbol NormalizePreviousDestination(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType ==
            SpecialType.System_Nullable_T)
        {
            return namedType.TypeArguments[0];
        }

        return type.IsReferenceType
            ? type.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
            : type;
    }

    private static string BuildConstructionPlanTypeName(
        INamedTypeSymbol destinationType,
        IReadOnlyDictionary<ITypeParameterSymbol, string> typeParameterNames)
    {
        var definition = destinationType.OriginalDefinition;
        var planNamespace =
            ConstructionSurfaceNaming.BuildPlanNamespace(definition);
        var planTypeName =
            ConstructionSurfaceNaming.BuildConstructionTypeName(definition);
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
