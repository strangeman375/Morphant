using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.ConstructionSurface.ConstructionPlan;

internal static class ConstructionPlanModelBuilder
{
    private const string AllowNullAttributeMetadataName =
        "System.Diagnostics.CodeAnalysis.AllowNullAttribute";

    private const string DisallowNullAttributeMetadataName =
        "System.Diagnostics.CodeAnalysis.DisallowNullAttribute";

    private const string ObsoleteAttributeMetadataName =
        "System.ObsoleteAttribute";

    private static readonly SymbolDisplayFormat DocumentationCrefFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions:
            SymbolDisplayMemberOptions.IncludeContainingType |
            SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions:
            SymbolDisplayParameterOptions.IncludeType |
            SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public static ConstructionPlanModel Build(
        INamedTypeSymbol destinationType,
        string planNamespace,
        string planTypeName,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        destinationType = destinationType.OriginalDefinition;

        var typeParameters = CollectTypeParameters(destinationType);
        var typeParameterNames =
            GeneratedTypeNameBuilder.AllocateTypeParameterNames(
                typeParameters);
        var constructors = BuildConstructors(
            destinationType,
            compilation,
            typeParameterNames,
            cancellationToken);

        return new ConstructionPlanModel(
            planNamespace,
            planTypeName,
            GeneratedPlanNaming.BuildConstructorParametersTypeName(
                destinationType),
            GeneratedTypeNameBuilder.Build(
                destinationType,
                typeParameterNames),
            BuildTypeParameters(
                typeParameters,
                typeParameterNames),
            BuildCref(destinationType),
            BuildObsoleteAttributeSource(destinationType),
            constructors,
            BuildConstructorParameterFields(constructors));
    }

    private static ImmutableArray<ITypeParameterSymbol> CollectTypeParameters(
        INamedTypeSymbol destinationType)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();

        for (var current = destinationType;
             current is not null;
             current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        var result = ImmutableArray.CreateBuilder<ITypeParameterSymbol>();

        while (containingTypes.Count > 0)
        {
            result.AddRange(containingTypes.Pop().TypeParameters);
        }

        return result.ToImmutable();
    }

    internal static ImmutableArray<ConstructionTypeParameterModel>
        BuildTypeParameters(
            ImmutableArray<ITypeParameterSymbol> typeParameters,
            IReadOnlyDictionary<ITypeParameterSymbol, string>
                typeParameterNames)
    {
        var result =
            ImmutableArray.CreateBuilder<ConstructionTypeParameterModel>(
                typeParameters.Length);

        foreach (var typeParameter in typeParameters)
        {
            var constraints = BuildTypeParameterConstraints(
                typeParameter,
                typeParameterNames,
                out var requiresNullableAnnotationsDisabled);

            result.Add(
                new ConstructionTypeParameterModel(
                    typeParameterNames[typeParameter],
                    constraints,
                    requiresNullableAnnotationsDisabled));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<string> BuildTypeParameterConstraints(
        ITypeParameterSymbol typeParameter,
        IReadOnlyDictionary<ITypeParameterSymbol, string> typeParameterNames,
        out bool requiresNullableAnnotationsDisabled)
    {
        var result = ImmutableArray.CreateBuilder<string>();
        requiresNullableAnnotationsDisabled = false;

        if (typeParameter.HasUnmanagedTypeConstraint)
        {
            result.Add("unmanaged");
        }
        else if (typeParameter.HasValueTypeConstraint)
        {
            result.Add("struct");
        }
        else if (typeParameter.HasReferenceTypeConstraint)
        {
            result.Add(
                typeParameter.ReferenceTypeConstraintNullableAnnotation ==
                NullableAnnotation.Annotated
                    ? "class?"
                    : "class");

            requiresNullableAnnotationsDisabled =
                typeParameter.ReferenceTypeConstraintNullableAnnotation ==
                NullableAnnotation.None;
        }
        else if (typeParameter.HasNotNullConstraint)
        {
            result.Add("notnull");
        }

        foreach (var constraintType in typeParameter.ConstraintTypes)
        {
            requiresNullableAnnotationsDisabled |=
                HasObliviousTopLevelAnnotation(constraintType);

            result.Add(
                GeneratedTypeNameBuilder.Build(
                    constraintType,
                    typeParameterNames));
        }

        if (typeParameter.HasConstructorConstraint &&
            !typeParameter.HasUnmanagedTypeConstraint &&
            !typeParameter.HasValueTypeConstraint)
        {
            result.Add("new()");
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<ConstructionConstructorModel>
        BuildConstructors(
            INamedTypeSymbol destinationType,
            Compilation compilation,
            IReadOnlyDictionary<ITypeParameterSymbol, string>
                typeParameterNames,
            CancellationToken cancellationToken)
    {
        var constructors =
            DestinationCapabilityPolicy.GetSupportedConstructors(
                destinationType,
                compilation,
                cancellationToken);
        var result =
            ImmutableArray.CreateBuilder<ConstructionConstructorModel>(
                constructors.Length);

        foreach (var constructor in constructors)
        {
            var parameters =
                ImmutableArray.CreateBuilder<
                    ConstructionConstructorParameterModel>(
                    constructor.Parameters.Length);

            foreach (var parameter in constructor.Parameters)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var typeName = BuildInputTypeName(
                    parameter.Type,
                    parameter.NullableAnnotation,
                    parameter,
                    typeParameterNames,
                    out var acceptsNull,
                    out var requiresNullableAnnotationsDisabled);

                parameters.Add(
                    new ConstructionConstructorParameterModel(
                        parameter.Name,
                        typeName,
                        BuildTypeSuffix(parameter.Type),
                        parameter.IsOptional || parameter.IsParams,
                        BuildDefaultValueDisplay(parameter),
                        acceptsNull,
                        requiresNullableAnnotationsDisabled));
            }

            result.Add(
                new ConstructionConstructorModel(
                    BuildObsoleteAttributeSource(constructor),
                    parameters.ToImmutable()));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<
            ConstructionConstructorParameterFieldModel>
        BuildConstructorParameterFields(
            ImmutableArray<ConstructionConstructorModel> constructors)
    {
        var uniqueParameters =
            new List<ConstructionConstructorParameterModel>();
        var parameterKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var constructor in constructors)
        {
            foreach (var parameter in constructor.Parameters)
            {
                var key = parameter.Name + "\0" + parameter.TypeName;

                if (parameterKeys.Add(key))
                {
                    uniqueParameters.Add(parameter);
                }
            }
        }

        var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var parameter in uniqueParameters)
        {
            nameCounts.TryGetValue(parameter.Name, out var count);
            nameCounts[parameter.Name] = count + 1;
        }

        var usedFieldNames = new HashSet<string>(StringComparer.Ordinal);
        var result =
            ImmutableArray.CreateBuilder<
                ConstructionConstructorParameterFieldModel>();

        foreach (var parameter in uniqueParameters)
        {
            var fieldName = nameCounts[parameter.Name] == 1
                ? parameter.Name
                : parameter.Name + parameter.TypeSuffix;

            fieldName = MakeUnique(fieldName, usedFieldNames);

            result.Add(
                new ConstructionConstructorParameterFieldModel(
                    fieldName,
                    parameter.Name,
                    parameter.TypeName,
                    parameter.AcceptsNull,
                    parameter.RequiresNullableAnnotationsDisabled));
        }

        return result.ToImmutable();
    }

    internal static string BuildInputTypeName(
        ITypeSymbol type,
        NullableAnnotation nullableAnnotation,
        ISymbol inputSymbol,
        IReadOnlyDictionary<ITypeParameterSymbol, string> typeParameterNames,
        out bool acceptsNull,
        out bool requiresNullableAnnotationsDisabled)
    {
        var hasDisallowNull = HasAttribute(
            inputSymbol,
            DisallowNullAttributeMetadataName);
        var hasAllowNull = HasAttribute(
            inputSymbol,
            AllowNullAttributeMetadataName);

        if (CanApplyNullableAnnotation(type))
        {
            if (hasDisallowNull)
            {
                nullableAnnotation = NullableAnnotation.NotAnnotated;
            }
            else if (hasAllowNull)
            {
                nullableAnnotation = NullableAnnotation.Annotated;
            }
        }

        acceptsNull = !hasDisallowNull &&
                      (IsNullableValueType(type) ||
                       nullableAnnotation == NullableAnnotation.Annotated ||
                       hasAllowNull && CanAcceptNull(type));

        requiresNullableAnnotationsDisabled =
            nullableAnnotation == NullableAnnotation.None &&
            CanApplyNullableAnnotation(type);

        return GeneratedTypeNameBuilder.Build(
            type.WithNullableAnnotation(nullableAnnotation),
            typeParameterNames);
    }

    private static bool CanApplyNullableAnnotation(ITypeSymbol type)
    {
        return type.IsReferenceType ||
               type.TypeKind == TypeKind.TypeParameter;
    }

    private static bool CanAcceptNull(ITypeSymbol type)
    {
        return CanApplyNullableAnnotation(type) ||
               IsNullableValueType(type);
    }

    private static bool IsNullableValueType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.SpecialType ==
               SpecialType.System_Nullable_T;
    }

    private static bool HasObliviousTopLevelAnnotation(ITypeSymbol type)
    {
        return CanApplyNullableAnnotation(type) &&
               type.NullableAnnotation == NullableAnnotation.None;
    }

    private static bool HasAttribute(
        ISymbol symbol,
        string attributeMetadataName)
    {
        return symbol.GetAttributes().Any(attribute =>
            HasMetadataName(
                attribute.AttributeClass,
                attributeMetadataName));
    }

    internal static string? BuildObsoleteAttributeSource(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!HasMetadataName(
                    attribute.AttributeClass,
                    ObsoleteAttributeMetadataName))
            {
                continue;
            }

            var arguments = new List<string>();

            foreach (var argument in attribute.ConstructorArguments)
            {
                arguments.Add(FormatAttributeArgument(argument));
            }

            foreach (var argument in attribute.NamedArguments)
            {
                arguments.Add(
                    EscapeIdentifier(argument.Key) +
                    " = " +
                    FormatAttributeArgument(argument.Value));
            }

            const string attributeTypeName =
                "global::System.ObsoleteAttribute";

            return arguments.Count == 0
                ? attributeTypeName
                : attributeTypeName +
                  "(" +
                  string.Join(", ", arguments) +
                  ")";
        }

        return null;
    }

    private static string FormatAttributeArgument(TypedConstant argument)
    {
        if (argument.IsNull)
        {
            return "null";
        }

        return SymbolDisplay.FormatPrimitive(
                   argument.Value!,
                   quoteStrings: true,
                   useHexadecimalNumbers: false)
               ?? "default";
    }

    private static bool HasMetadataName(
        INamedTypeSymbol? type,
        string metadataName)
    {
        return type is not null &&
               SymbolNameHelper.GetFullMetadataName(type) == metadataName;
    }

    private static string BuildCref(ISymbol symbol)
    {
        var documentationSymbol = symbol.OriginalDefinition;

        return documentationSymbol.ToDisplayString(DocumentationCrefFormat);
    }

    private static string MakeUnique(
        string candidate,
        HashSet<string> usedNames)
    {
        if (usedNames.Add(candidate))
        {
            return candidate;
        }

        for (var suffix = 2;; suffix++)
        {
            var name = candidate +
                       suffix.ToString(CultureInfo.InvariantCulture);

            if (usedNames.Add(name))
            {
                return name;
            }
        }
    }

    private static string BuildTypeSuffix(ITypeSymbol type)
    {
        var displayName = type.ToDisplayString(
            SymbolDisplayFormat.MinimallyQualifiedFormat);
        var result = new StringBuilder(displayName.Length);
        var uppercaseNext = true;

        for (var index = 0; index < displayName.Length; index++)
        {
            var character = displayName[index];

            if (char.IsLetterOrDigit(character) || character == '_')
            {
                result.Append(
                    uppercaseNext
                        ? char.ToUpperInvariant(character)
                        : character);
                uppercaseNext = false;
                continue;
            }

            if (character == '?')
            {
                result.Append("Nullable");
                uppercaseNext = true;
                continue;
            }

            if (character == '[')
            {
                var rank = 1;

                while (++index < displayName.Length &&
                       displayName[index] != ']')
                {
                    if (displayName[index] == ',')
                    {
                        rank++;
                    }
                }

                result.Append("Array");

                if (rank > 1)
                {
                    result.Append(
                        rank.ToString(CultureInfo.InvariantCulture));
                }
            }

            uppercaseNext = true;
        }

        return result.Length == 0
            ? "Value"
            : result.ToString();
    }

    private static string? BuildDefaultValueDisplay(
        IParameterSymbol parameter)
    {
        if (!parameter.HasExplicitDefaultValue)
        {
            return null;
        }

        var value = parameter.ExplicitDefaultValue;

        if (value is null)
        {
            return CanRepresentNull(parameter.Type)
                ? "null"
                : "default";
        }

        if (parameter.Type is INamedTypeSymbol
            {
                TypeKind: TypeKind.Enum
            } enumType)
        {
            return BuildEnumDefaultValueDisplay(enumType, value);
        }

        return SymbolDisplay.FormatPrimitive(
                   value,
                   quoteStrings: true,
                   useHexadecimalNumbers: false)
               ?? Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static string BuildEnumDefaultValueDisplay(
        INamedTypeSymbol enumType,
        object value)
    {
        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
        {
            if (member.HasConstantValue &&
                object.Equals(member.ConstantValue, value))
            {
                return enumType.ToDisplayString(
                           SymbolDisplayFormat.MinimallyQualifiedFormat) +
                       "." +
                       EscapeIdentifier(member.Name);
            }
        }

        var numericValue = SymbolDisplay.FormatPrimitive(
                               value,
                               quoteStrings: true,
                               useHexadecimalNumbers: false)
                           ?? Convert.ToString(
                               value,
                               CultureInfo.InvariantCulture)
                           ?? "0";

        return "(" +
               enumType.ToDisplayString(
                   SymbolDisplayFormat.MinimallyQualifiedFormat) +
               ")" +
               numericValue;
    }

    private static bool CanRepresentNull(ITypeSymbol type)
    {
        return type.IsReferenceType ||
               type is IPointerTypeSymbol ||
               type is INamedTypeSymbol
               {
                   OriginalDefinition.SpecialType:
                       SpecialType.System_Nullable_T
               };
    }

    internal static string EscapeIdentifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value
            : value;
    }
}
