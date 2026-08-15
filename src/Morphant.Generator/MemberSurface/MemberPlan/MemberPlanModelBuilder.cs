using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.MemberSurface.MemberPlan;

internal static class MemberPlanModelBuilder
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
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public static MemberPlanModel Build(
        INamedTypeSymbol destinationType,
        bool includeInitOnlyProperties,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        destinationType = destinationType.OriginalDefinition;

        var typeParameters = CollectTypeParameters(destinationType);
        var typeParameterNames =
            GeneratedTypeNameBuilder.AllocateTypeParameterNames(
                typeParameters);
        var members = DestinationMemberPolicy.GetSupportedMembers(
            destinationType,
            compilation,
            includeInitOnlyProperties,
            cancellationToken);

        return new MemberPlanModel(
            GeneratedPlanNaming.BuildNamespace(destinationType),
            GeneratedPlanNaming.BuildMembersTypeName(destinationType),
            BuildTypeParameters(
                typeParameters,
                typeParameterNames),
            BuildCref(destinationType),
            BuildObsoleteAttributeSource(destinationType),
            BuildMembers(
                members,
                typeParameterNames,
                includeInitOnlyProperties,
                compilation,
                cancellationToken));
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

    private static ImmutableArray<MemberPlanTypeParameterModel>
        BuildTypeParameters(
            ImmutableArray<ITypeParameterSymbol> typeParameters,
            IReadOnlyDictionary<ITypeParameterSymbol, string>
                typeParameterNames)
    {
        var result =
            ImmutableArray.CreateBuilder<MemberPlanTypeParameterModel>(
                typeParameters.Length);

        foreach (var typeParameter in typeParameters)
        {
            var constraints = BuildTypeParameterConstraints(
                typeParameter,
                typeParameterNames,
                out var requiresNullableAnnotationsDisabled);

            result.Add(
                new MemberPlanTypeParameterModel(
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
                    typeParameterNames,
                    normalizeDynamic: false));
        }

        if (typeParameter.HasConstructorConstraint &&
            !typeParameter.HasUnmanagedTypeConstraint &&
            !typeParameter.HasValueTypeConstraint)
        {
            result.Add("new()");
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<MemberPlanPropertyModel> BuildMembers(
        ImmutableArray<ISymbol> members,
        IReadOnlyDictionary<ITypeParameterSymbol, string> typeParameterNames,
        bool includeInitOnlyProperties,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var result =
            ImmutableArray.CreateBuilder<MemberPlanPropertyModel>(
                members.Length);

        foreach (var member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (member is IPropertySymbol property)
            {
                var setter = property.SetMethod;
                var canWrite = setter is not null &&
                    (includeInitOnlyProperties || !setter.IsInitOnly) &&
                    compilation.IsSymbolAccessibleWithin(
                        setter,
                        compilation.Assembly);
                var setterParameter = canWrite
                    ? setter!.Parameters[setter.Parameters.Length - 1]
                    : null;
                var typeName = BuildInputTypeName(
                    property.Type,
                    setterParameter?.NullableAnnotation ??
                    property.NullableAnnotation,
                    property,
                    setterParameter,
                    typeParameterNames,
                    out var acceptsNull,
                    out var requiresNullableAnnotationsDisabled);

                result.Add(
                    new MemberPlanPropertyModel(
                        property.Name,
                        typeName,
                        BuildCref(property),
                        canWrite,
                        acceptsNull,
                        requiresNullableAnnotationsDisabled,
                        BuildObsoleteAttributeSource(property)));
                continue;
            }

            var field = (IFieldSymbol)member;
            var fieldTypeName = BuildInputTypeName(
                field.Type,
                field.NullableAnnotation,
                field,
                null,
                typeParameterNames,
                out var fieldAcceptsNull,
                out var fieldRequiresNullableAnnotationsDisabled);

            result.Add(
                new MemberPlanPropertyModel(
                    field.Name,
                    fieldTypeName,
                    BuildCref(field),
                    !field.IsReadOnly,
                    fieldAcceptsNull,
                    fieldRequiresNullableAnnotationsDisabled,
                    BuildObsoleteAttributeSource(field)));
        }

        return result.ToImmutable();
    }

    private static string BuildInputTypeName(
        ITypeSymbol type,
        NullableAnnotation nullableAnnotation,
        ISymbol member,
        ISymbol? inputSymbol,
        IReadOnlyDictionary<ITypeParameterSymbol, string> typeParameterNames,
        out bool acceptsNull,
        out bool requiresNullableAnnotationsDisabled)
    {
        var hasDisallowNull =
            HasAttribute(
                member,
                DisallowNullAttributeMetadataName) ||
            HasAttribute(
                inputSymbol,
                DisallowNullAttributeMetadataName);
        var hasAllowNull =
            HasAttribute(
                member,
                AllowNullAttributeMetadataName) ||
            HasAttribute(
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
            typeParameterNames,
            normalizeDynamic: false);
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
        ISymbol? symbol,
        string attributeMetadataName)
    {
        if (symbol is null)
        {
            return false;
        }

        foreach (var attribute in symbol.GetAttributes())
        {
            if (HasMetadataName(
                    attribute.AttributeClass,
                    attributeMetadataName))
            {
                return true;
            }
        }

        return false;
    }

    private static string? BuildObsoleteAttributeSource(ISymbol symbol)
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
                    Identifier(argument.Key) +
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

    private static string Identifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value
            : value;
    }
}
