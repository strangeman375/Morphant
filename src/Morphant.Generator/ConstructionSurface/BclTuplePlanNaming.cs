using Microsoft.CodeAnalysis;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.ConstructionSurface;

internal static class BclTuplePlanNaming
{
    public static string BuildNamespace(
        BclTupleShape shape,
        Compilation compilation)
    {
        // Reuse the complete tuple contract/presentation fingerprint that
        // also identifies its hint file, then scope it to this assembly.
        return GeneratedPlanNaming.BuildNamespace(
            "tuple:" + BuildHintIdentity(shape), compilation);
    }

    public static string BuildStableIdentity(BclTupleShape shape)
    {
        var sourceContract =
            MappingTypeIdentityPolicy.Create(shape.Type).DisplayName;

        return sourceContract + "|" +
               BclTupleShapePolicy.BuildPresentationKey(shape.Type);
    }

    public static string BuildConstructionTypeName(BclTupleShape shape)
    {
        return "TupleConstruction";
    }

    public static string BuildConstructorParametersTypeName(
        BclTupleShape shape)
    {
        return "TupleConstructorParameters";
    }

    public static string BuildMembersTypeName(BclTupleShape shape)
    {
        return "TupleMembers";
    }

    public static string BuildPlanTypeReference(
        BclTupleShape shape,
        string typeName,
        Compilation compilation,
        IReadOnlyDictionary<ITypeParameterSymbol, string>
            availableTypeParameterNames)
    {
        var typeParameters = GeneratedTypeNameBuilder.CollectTypeParameters(
            shape.Type);

        return "global::" +
               BuildNamespace(shape, compilation) +
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
        return BuildNamespaceName(shape);
    }

    private static string BuildNamespaceName(BclTupleShape shape)
    {
        return (shape.Kind == BclTupleKind.ValueTuple ? "V" : "S") +
               shape.Elements.Length.ToString(
                   System.Globalization.CultureInfo.InvariantCulture) +
               "_" +
               HintNameHelper.GetStableHash128(BuildStableIdentity(shape));
    }
}
