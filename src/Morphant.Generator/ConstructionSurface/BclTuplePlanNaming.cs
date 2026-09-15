using Microsoft.CodeAnalysis;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.ConstructionSurface;

internal static class BclTuplePlanNaming
{
    public static string BuildNamespace(
        BclTupleShape shape,
        Compilation compilation)
    {
        return GeneratedPlanNaming.RootNamespace + ".N_" +
               BuildIdentity(shape, compilation);
    }

    public static string BuildIdentity(
        BclTupleShape shape,
        Compilation compilation)
    {
        // Preserve the established tuple namespace fingerprint independently
        // of the readable filename or its length limit.
        return GeneratedEntityIdentity.Create(
            "tuple:" + BuildContractFingerprint(shape), compilation);
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

    private static string BuildContractFingerprint(BclTupleShape shape)
    {
        return (shape.Kind == BclTupleKind.ValueTuple ? "V" : "S") +
               shape.Elements.Length.ToString(
                   System.Globalization.CultureInfo.InvariantCulture) +
               "_" +
               HintNameHelper.GetStableHash128(BuildStableIdentity(shape));
    }
}
