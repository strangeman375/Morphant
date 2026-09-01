using System.Text;
using Microsoft.CodeAnalysis;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.ConstructionSurface;

internal static class BclTuplePlanNaming
{
    private const string RootNamespace = "Morphant.Generated.Tuples";

    // The root plus two identifiers at this limit stays below C#'s
    // 1024-character fully-qualified type-name limit.
    private const int MaxGeneratedIdentifierLength = 480;

    public static string BuildNamespace(BclTupleShape shape)
    {
        return RootNamespace + "." + BuildNamespaceContractName(shape);
    }

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
        return BuildPlanTypeName(shape, "Construction");
    }

    public static string BuildConstructorParametersTypeName(
        BclTupleShape shape)
    {
        return BuildPlanTypeName(shape, "ConstructorParameters");
    }

    public static string BuildMembersTypeName(BclTupleShape shape)
    {
        return BuildPlanTypeName(shape, "Members");
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
               BuildNamespace(shape) +
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
        return BuildNamespaceContractName(shape) +
               "." +
               BuildPlanTypeName(shape, suffix: string.Empty);
    }

    public static bool IsSystemTuplePlanNamespace(string value)
    {
        const string prefix = RootNamespace + ".SystemTuple";

        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var arityStart = prefix.Length;

        return value.Length > arityStart &&
               char.IsDigit(value[arityStart]);
    }

    private static string BuildReadablePrefix(BclTupleShape shape)
    {
        return (shape.Kind == BclTupleKind.ValueTuple
                ? "ValueTuple"
                : "SystemTuple") +
               shape.Elements.Length.ToString(
                   System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string BuildPlanTypeName(
        BclTupleShape shape,
        string suffix)
    {
        if (shape.Kind == BclTupleKind.SystemTuple ||
            shape.Elements.IsEmpty)
        {
            return "Tuple" + suffix;
        }

        var presentation = BuildValueTuplePresentationName(shape);
        var typeSuffix = suffix.Length == 0
            ? string.Empty
            : "_" + suffix;
        var boundedPresentation = HintNameHelper.LimitWithStableHash(
            presentation,
            presentation,
            MaxGeneratedIdentifierLength - typeSuffix.Length);

        return boundedPresentation + typeSuffix;
    }

    private static string BuildNamespaceContractName(BclTupleShape shape)
    {
        var contract = BuildTupleContractName(
            shape,
            includePresentation: false);

        return HintNameHelper.LimitWithStableHash(
            contract,
            contract,
            MaxGeneratedIdentifierLength);
    }

    private static string BuildTupleContractName(
        BclTupleShape shape,
        bool includePresentation)
    {
        var result = new StringBuilder();

        result.Append(
                shape.Kind == BclTupleKind.ValueTuple
                    ? "ValueTuple"
                    : "SystemTuple")
            .Append(shape.Elements.Length.ToString(
                System.Globalization.CultureInfo.InvariantCulture));

        foreach (var element in shape.Elements)
        {
            result.Append('_')
                .Append(BuildTypeContractName(element.Type));
        }

        if (includePresentation &&
            shape.Kind == BclTupleKind.ValueTuple &&
            !shape.Elements.IsEmpty)
        {
            result.Append('_')
                .Append(BuildValueTuplePresentationName(shape));
        }

        return result.ToString();
    }

    private static string BuildValueTuplePresentationName(
        BclTupleShape shape)
    {
        return "Tuple_" +
               string.Join(
                   "_",
                   shape.Elements.Select(static element => element.Name));
    }

    private static string BuildTypeContractName(ITypeSymbol type)
    {
        if (type is IDynamicTypeSymbol)
        {
            return AddNullableSuffix(type, "Dynamic");
        }

        if (type is IArrayTypeSymbol array)
        {
            var kind = array.IsSZArray ? "Array" : "MdArray";
            var value = kind +
                        array.Rank.ToString(
                            System.Globalization.CultureInfo.InvariantCulture) +
                        "_" +
                        BuildTypeContractName(array.ElementType);

            return AddNullableSuffix(type, value);
        }

        if (type is ITypeParameterSymbol typeParameter)
        {
            return AddNullableSuffix(type, typeParameter.Name);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return AddNullableSuffix(
                type,
                HintNameHelper.ToHintNamePart(type.ToDisplayString()));
        }

        if (namedType.OriginalDefinition.SpecialType ==
            SpecialType.System_Nullable_T)
        {
            return BuildTypeContractName(namedType.TypeArguments[0]) +
                   "Nullable";
        }

        if (BclTupleShapePolicy.TryCreate(namedType) is { } tuple)
        {
            return AddNullableSuffix(
                type,
                BuildTupleContractName(
                    tuple,
                    includePresentation: true));
        }

        var name = IsPredefinedScalarType(namedType.SpecialType)
            ? namedType.Name
            : BuildNamedTypeContractName(namedType);

        return AddNullableSuffix(type, name);
    }

    private static bool IsPredefinedScalarType(SpecialType type)
    {
        return type is
            SpecialType.System_Object or
            SpecialType.System_Boolean or
            SpecialType.System_Char or
            SpecialType.System_SByte or
            SpecialType.System_Byte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_IntPtr or
            SpecialType.System_UIntPtr or
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_Decimal or
            SpecialType.System_String;
    }

    private static string BuildNamedTypeContractName(INamedTypeSymbol type)
    {
        var typeParts = new Stack<string>();

        for (var current = type;
             current is not null;
             current = current.ContainingType)
        {
            var part = EscapeName(current.Name) +
                       (current.Arity == 0
                           ? string.Empty
                           : current.Arity.ToString(
                               System.Globalization.CultureInfo.InvariantCulture));

            if (!current.TypeArguments.IsEmpty)
            {
                part += "_" +
                        string.Join(
                            "_",
                            current.TypeArguments.Select(
                                BuildTypeContractName));
            }

            typeParts.Push(part);
        }

        var parts = new List<string>();

        if (!type.ContainingNamespace.IsGlobalNamespace)
        {
            var namespaceParts = new Stack<string>();

            for (var current = type.ContainingNamespace;
                 !current.IsGlobalNamespace;
                 current = current.ContainingNamespace)
            {
                namespaceParts.Push(EscapeName(current.Name));
            }

            foreach (var part in namespaceParts)
            {
                parts.Add(part);
            }
        }

        parts.AddRange(typeParts);

        return "Type_" + string.Join("_", parts);
    }

    private static string AddNullableSuffix(
        ITypeSymbol type,
        string value)
    {
        return type.NullableAnnotation == NullableAnnotation.Annotated
            ? value + "Nullable"
            : value;
    }

    private static string EscapeName(string value)
    {
        return value.Replace("_", "__");
    }
}
