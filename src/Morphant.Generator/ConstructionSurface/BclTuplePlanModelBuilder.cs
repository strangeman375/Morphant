using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Morphant.Generator.ConstructionSurface.ConstructionPlan;
using Morphant.Generator.ConstructionSurface.PairConfiguration;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MemberSurface.MemberPlan;

namespace Morphant.Generator.ConstructionSurface;

internal static class BclTuplePlanModelBuilder
{
    public static ConstructionPlanModel BuildConstruction(
        BclTupleShape shape,
        Compilation compilation)
    {
        var typeParameters = GeneratedTypeNameBuilder.CollectTypeParameters(
            shape.Type);
        var typeParameterNames =
            GeneratedTypeNameBuilder.AllocateTypeParameterNames(
                typeParameters);
        var planTypeParameters = PairTypeParameterModelBuilder.Build(
            shape.Type,
            shape.Type,
            typeParameters,
            typeParameterNames,
            compilation);
        var constructorParameters = shape.Elements
            .Select(element => BuildConstructorParameter(
                element,
                typeParameterNames))
            .ToImmutableArray();
        var fields = constructorParameters
            .Select(parameter =>
                new ConstructionConstructorParameterFieldModel(
                    parameter.Name,
                    parameter.Name,
                    parameter.TypeName,
                    parameter.AcceptsNull,
                    parameter.RequiresNullableAnnotationsDisabled))
            .ToImmutableArray();

        return new ConstructionPlanModel(
            BclTuplePlanNaming.BuildNamespace(shape),
            BclTuplePlanNaming.BuildConstructionTypeName(shape),
            BclTuplePlanNaming.BuildConstructorParametersTypeName(shape),
            BuildPhysicalTypeName(shape, typeParameterNames),
            planTypeParameters,
            BuildDestinationCref(shape),
            ObsoleteAttributeSource: null,
            ImmutableArray.Create(
                new ConstructionConstructorModel(
                    ObsoleteAttributeSource: null,
                    constructorParameters)),
            fields);
    }

    public static MemberPlanModel BuildMembers(
        BclTupleShape shape,
        Compilation compilation)
    {
        var typeParameters = GeneratedTypeNameBuilder.CollectTypeParameters(
            shape.Type);
        var typeParameterNames =
            GeneratedTypeNameBuilder.AllocateTypeParameterNames(
                typeParameters);
        var constructionTypeParameters = PairTypeParameterModelBuilder.Build(
            shape.Type,
            shape.Type,
            typeParameters,
            typeParameterNames,
            compilation);
        var memberTypeParameters = constructionTypeParameters
            .Select(static parameter => new MemberPlanTypeParameterModel(
                parameter.Name,
                parameter.Constraints,
                parameter.RequiresNullableAnnotationsDisabled))
            .ToImmutableArray();
        var members = shape.Elements
            .Select(element => BuildMember(
                element,
                typeParameterNames))
            .ToImmutableArray();

        return new MemberPlanModel(
            BclTuplePlanNaming.BuildNamespace(shape),
            BclTuplePlanNaming.BuildMembersTypeName(shape),
            memberTypeParameters,
            BuildDestinationCref(shape),
            ObsoleteAttributeSource: null,
            members);
    }

    private static ConstructionConstructorParameterModel
        BuildConstructorParameter(
            BclTupleElement element,
            IReadOnlyDictionary<ITypeParameterSymbol, string>
                typeParameterNames)
    {
        var typeName = ConstructionPlanModelBuilder.BuildInputTypeName(
            element.Type,
            element.Type.NullableAnnotation,
            element.Symbol,
            typeParameterNames,
            out var acceptsNull,
            out var requiresNullableAnnotationsDisabled);

        return new ConstructionConstructorParameterModel(
            element.Name,
            typeName,
            "Element" + element.Ordinal.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            IsOptional: false,
            DefaultValueDisplay: null,
            acceptsNull,
            requiresNullableAnnotationsDisabled);
    }

    private static MemberPlanPropertyModel BuildMember(
        BclTupleElement element,
        IReadOnlyDictionary<ITypeParameterSymbol, string>
            typeParameterNames)
    {
        var typeName = MemberPlanModelBuilder.BuildInputTypeName(
            element.Type,
            element.Type.NullableAnnotation,
            element.Symbol,
            inputSymbol: null,
            typeParameterNames,
            out var acceptsNull,
            out var requiresNullableAnnotationsDisabled);

        return new MemberPlanPropertyModel(
            element.Name,
            typeName,
            Cref: null,
            CanWrite: true,
            acceptsNull,
            requiresNullableAnnotationsDisabled,
            ObsoleteAttributeSource: null);
    }

    private static string BuildPhysicalTypeName(
        BclTupleShape shape,
        IReadOnlyDictionary<ITypeParameterSymbol, string>
            typeParameterNames)
    {
        var type = shape.Type.IsTupleType
            ? shape.Type.TupleUnderlyingType ?? shape.Type
            : shape.Type;

        return GeneratedTypeNameBuilder.Build(
            type,
            typeParameterNames,
            normalizeDynamic: false);
    }

    private static string BuildDestinationCref(BclTupleShape shape)
    {
        var type = shape.Type.IsTupleType
            ? shape.Type.TupleUnderlyingType ?? shape.Type
            : shape.Type;

        return type.OriginalDefinition.GetDocumentationCommentId() ??
               SymbolNameHelper.GetFullMetadataName(
                   type.OriginalDefinition);
    }
}
