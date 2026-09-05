using Microsoft.CodeAnalysis;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.ConstructionSurface;

internal static class BclTuplePlanNaming
{
    private const string RootNamespace = "Morphant.Generated.Tuples";

    public static string BuildNamespace(
        BclTupleShape shape,
        Compilation compilation)
    {
        return RootNamespace + "." +
               GeneratedAssemblyNaming.BuildScope(compilation) + "." +
               BuildNamespaceName(shape);
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

    public static bool IsSystemTuplePlanNamespace(string value)
    {
        var prefix = RootNamespace + ".A_";

        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var shapeStart = value.IndexOf('.', prefix.Length) + 1;

        if (shapeStart == 0 || shapeStart >= value.Length ||
            value[shapeStart] != 'S')
        {
            return false;
        }

        var index = shapeStart + 1;

        while (index < value.Length && char.IsDigit(value[index]))
        {
            index++;
        }

        return index > shapeStart + 1 &&
               index < value.Length &&
               value[index] == '_';
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
