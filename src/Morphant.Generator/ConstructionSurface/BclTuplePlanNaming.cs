using Microsoft.CodeAnalysis;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.ConstructionSurface;

internal static class BclTuplePlanNaming
{
    public const string Namespace = "Morphant.Generated.Tuples";

    public static string BuildStableIdentity(BclTupleShape shape)
    {
        var physicalType = shape.Type.IsTupleType
            ? shape.Type.TupleUnderlyingType ?? shape.Type
            : shape.Type;
        var typeParameters = GeneratedTypeNameBuilder.CollectTypeParameters(
            physicalType);
        var typeParameterNames =
            GeneratedTypeNameBuilder.AllocateTypeParameterNames(
                typeParameters);
        var physicalIdentity = GeneratedTypeNameBuilder.Build(
            physicalType,
            typeParameterNames,
            normalizeDynamic: false);

        return BuildReadablePrefix(shape) + "_" +
               HintNameHelper.GetStableHash(
                   physicalIdentity + "|" + shape.PlanIdentity);
    }

    public static string BuildConstructionTypeName(BclTupleShape shape)
    {
        return BuildStableIdentity(shape) + "Construction";
    }

    public static string BuildConstructorParametersTypeName(
        BclTupleShape shape)
    {
        return BuildStableIdentity(shape) + "ConstructorParameters";
    }

    public static string BuildMembersTypeName(BclTupleShape shape)
    {
        return BuildStableIdentity(shape) + "Members";
    }

    public static string BuildPlanTypeReference(
        BclTupleShape shape,
        string typeName,
        IReadOnlyDictionary<ITypeParameterSymbol, string>
            availableTypeParameterNames)
    {
        var typeParameters = GeneratedTypeNameBuilder.CollectTypeParameters(
            shape.Type);

        return "global::" +
               Namespace +
               "." +
               typeName +
               (typeParameters.IsEmpty
                   ? string.Empty
                   : "<" +
                     string.Join(
                         ", ",
                         typeParameters.Select(typeParameter =>
                             GeneratedTypeNameBuilder.Build(
                                 typeParameter,
                                 availableTypeParameterNames))) +
                     ">");
    }

    public static string BuildHintIdentity(BclTupleShape shape)
    {
        return BuildStableIdentity(shape);
    }

    private static string BuildReadablePrefix(BclTupleShape shape)
    {
        return (shape.Kind == BclTupleKind.ValueTuple
                ? "ValueTuple"
                : "SystemTuple") +
               shape.Elements.Length.ToString(
                   System.Globalization.CultureInfo.InvariantCulture);
    }
}
