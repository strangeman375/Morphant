using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperBuilderMap;

namespace Morphant.Generator.TemplateSurface;

internal static class TemplateDestinationTypePipeline
{
    public static IncrementalValuesProvider<TemplateDestinationTypeInfo> Build(
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValuesProvider<MapperBuilderMapInfo> mapInfos)
    {
        var destinationTypes = mapInfos
            .Combine(compilationContext)
            .SelectMany(static (source, cancellationToken) =>
            {
                var (mapInfo, context) = source;

                return BuildDestinationTypes(
                    mapInfo,
                    context,
                    cancellationToken);
            })
            .WithTrackingName(
                MorphantGeneratorStageNames
                    .BuildTemplateDestinationTypeInfos);

        return destinationTypes
            .Collect()
            .SelectMany(static (destinationTypes, cancellationToken) =>
                DeduplicateAndSort(
                    destinationTypes,
                    cancellationToken))
            .WithTrackingName(
                MorphantGeneratorStageNames
                    .CollectTemplateDestinationTypeInfos);
    }

    private static ImmutableArray<TemplateDestinationTypeInfo>
        BuildDestinationTypes(
            MapperBuilderMapInfo mapInfo,
            CompilationContext context,
            CancellationToken cancellationToken)
    {
        var semanticModel = context.Compilation.GetSemanticModel(
            mapInfo.ConfigureSyntax.SyntaxTree);

        var result =
            ImmutableArray.CreateBuilder<TemplateDestinationTypeInfo>();

        foreach (var registration in mapInfo.Registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (MappingTypePolicy.IsSupported(
                    registration.SourceType) &&
                MappingTypePolicy.IsSupported(
                    registration.DestinationType) &&
                TryGetDestinationType(
                    registration.Syntax,
                    semanticModel,
                    cancellationToken) is { } destinationType)
            {
                result.Add(destinationType);
            }
        }

        return result.ToImmutable();
    }

    private static TemplateDestinationTypeInfo? TryGetDestinationType(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax genericName
            })
        {
            return null;
        }

        var destinationTypeSyntax =
            genericName.TypeArgumentList.Arguments[1];

        var destinationType = semanticModel.GetTypeInfo(
            destinationTypeSyntax,
            cancellationToken).Type;

        if (destinationType is null)
        {
            return null;
        }

        if (destinationType is IDynamicTypeSymbol)
        {
            return BuildDestinationTypeInfo(
                PreserveTopLevelNullableAnnotation(
                    destinationTypeSyntax,
                    semanticModel.Compilation.GetSpecialType(
                        SpecialType.System_Object)),
                TemplateDestinationTypeKind.DirectTemplate);
        }

        if (destinationType is not INamedTypeSymbol namedDestinationType)
        {
            return null;
        }

        namedDestinationType = PreserveTopLevelNullableAnnotation(
            destinationTypeSyntax,
            namedDestinationType);

        if (GetDestinationTypeKind(
                namedDestinationType,
                semanticModel.Compilation) is not { } kind)
        {
            return null;
        }

        return BuildDestinationTypeInfo(
            namedDestinationType,
            kind);
    }

    private static INamedTypeSymbol PreserveTopLevelNullableAnnotation(
        TypeSyntax syntax,
        INamedTypeSymbol type)
    {
        return syntax is NullableTypeSyntax && type.IsReferenceType
            ? (INamedTypeSymbol)type.WithNullableAnnotation(
                NullableAnnotation.Annotated)
            : type;
    }

    private static TemplateDestinationTypeInfo BuildDestinationTypeInfo(
        INamedTypeSymbol destinationType,
        TemplateDestinationTypeKind kind)
    {
        var templateExtensionSignature =
            BuildTemplateExtensionSignature(destinationType);

        var usageDefinition = destinationType.OriginalDefinition;

        var usageMetadataName =
            SymbolNameHelper.GetFullMetadataName(usageDefinition);

        var usageTypeArgumentList =
            BuildTemplateTypeArgumentList(destinationType);

        var usageIdentity =
            usageMetadataName + usageTypeArgumentList;

        var fullyQualifiedName = destinationType.ToDisplayString(
            SymbolDisplayFormats.FullyQualifiedNullable);

        var existingDestinationTypeFullyQualifiedName =
            destinationType.IsReferenceType
                ? destinationType
                    .WithNullableAnnotation(NullableAnnotation.Annotated)
                    .ToDisplayString(
                        SymbolDisplayFormats.FullyQualifiedNullable)
                : fullyQualifiedName;

        if (kind == TemplateDestinationTypeKind.DirectTemplate)
        {
            return new TemplateDestinationTypeInfo(
                kind,
                null,
                templateExtensionSignature,
                usageIdentity,
                fullyQualifiedName,
                existingDestinationTypeFullyQualifiedName,
                fullyQualifiedName,
                CanGenerateTemplateExtension(destinationType));
        }

        var templateDestinationType =
            GetTemplateDestinationType(destinationType);

        var definition = templateDestinationType.OriginalDefinition;

        var definitionMetadataName =
            SymbolNameHelper.GetFullMetadataName(definition);

        var templateTypeArgumentList =
            BuildTemplateTypeArgumentList(templateDestinationType);

        var templateNamespace =
            BuildTemplateNamespace(definition);

        var templateTypeName =
            definition.Name + "MorphantTemplate";

        var templateTypeFullyQualifiedName =
            "global::" +
            templateNamespace +
            "." +
            templateTypeName +
            templateTypeArgumentList +
            (IsNullableDestination(destinationType) ? "?" : string.Empty);

        return new TemplateDestinationTypeInfo(
            kind,
            new TemplateTypeDefinitionInfo(
                definitionMetadataName,
                templateNamespace,
                templateTypeName),
            templateExtensionSignature,
            usageIdentity,
            fullyQualifiedName,
            existingDestinationTypeFullyQualifiedName,
            templateTypeFullyQualifiedName,
            CanGenerateTemplateExtension(destinationType));
    }

    private static TemplateExtensionSignatureInfo
        BuildTemplateExtensionSignature(ITypeSymbol destinationType)
    {
        var preference = GetTemplateExtensionSignaturePreference(
            destinationType);

        return new TemplateExtensionSignatureInfo(
            DocumentationCommentId.CreateReferenceId(
                destinationType) ??
            destinationType.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat),
            preference.DynamicTypeCount,
            preference.NullableReferenceTypeCount,
            preference.ExplicitTupleElementNameCount);
    }

    private static TemplateExtensionSignaturePreference
        GetTemplateExtensionSignaturePreference(ITypeSymbol type)
    {
        if (type is IDynamicTypeSymbol)
        {
            return new TemplateExtensionSignaturePreference(
                DynamicTypeCount: 1,
                NullableReferenceTypeCount: 0,
                ExplicitTupleElementNameCount: 0);
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return GetTemplateExtensionSignaturePreference(
                arrayType.ElementType);
        }

        if (type is IPointerTypeSymbol pointerType)
        {
            return GetTemplateExtensionSignaturePreference(
                pointerType.PointedAtType);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return default;
        }

        var preference = new TemplateExtensionSignaturePreference(
            DynamicTypeCount: 0,
            NullableReferenceTypeCount:
                namedType.IsReferenceType &&
                namedType.NullableAnnotation ==
                NullableAnnotation.Annotated
                    ? 1
                    : 0,
            ExplicitTupleElementNameCount:
                namedType.IsTupleType
                    ? namedType.TupleElements.Count(static element =>
                        element.IsExplicitlyNamedTupleElement)
                    : 0);

        if (namedType.ContainingType is { } containingType)
        {
            preference += GetTemplateExtensionSignaturePreference(
                containingType);
        }

        foreach (var typeArgument in namedType.TypeArguments)
        {
            preference += GetTemplateExtensionSignaturePreference(
                typeArgument);
        }

        return preference;
    }

    private static string BuildTemplateTypeArgumentList(
        INamedTypeSymbol destinationType)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();

        for (var current = destinationType;
             current is not null;
             current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        var typeArguments = new List<string>();

        while (containingTypes.Count > 0)
        {
            foreach (var typeArgument in containingTypes.Pop().TypeArguments)
            {
                typeArguments.Add(
                    typeArgument.ToDisplayString(
                        SymbolDisplayFormats.FullyQualifiedNullable));
            }
        }

        return typeArguments.Count == 0
            ? string.Empty
            : "<" + string.Join(", ", typeArguments) + ">";
    }

    private static string BuildTemplateNamespace(
        INamedTypeSymbol destinationType)
    {
        var destinationNamespace =
            destinationType.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : destinationType.ContainingNamespace.ToDisplayString();

        var templateNamespace =
            string.IsNullOrEmpty(destinationNamespace)
                ? "Morphant.Generated"
                : destinationNamespace + ".Morphant.Generated";

        if (destinationType.ContainingType is null)
        {
            return templateNamespace;
        }

        var containingTypeScopes = new Stack<string>();

        for (var containingType = destinationType.ContainingType;
             containingType is not null;
             containingType = containingType.ContainingType)
        {
            var aritySuffix = containingType.Arity == 0
                ? string.Empty
                : containingType.Arity.ToString(
                    CultureInfo.InvariantCulture);

            containingTypeScopes.Push(
                containingType.Name + aritySuffix + "Scope");
        }

        return templateNamespace + "." +
               string.Join(".", containingTypeScopes);
    }

    private static ImmutableArray<TemplateDestinationTypeInfo>
        DeduplicateAndSort(
            ImmutableArray<TemplateDestinationTypeInfo> destinationTypes,
            CancellationToken cancellationToken)
    {
        var orderedDestinationTypes = destinationTypes.ToArray();

        Array.Sort(
            orderedDestinationTypes,
            static (left, right) =>
            {
                var comparison = StringComparer.Ordinal.Compare(
                    left.UsageIdentity,
                    right.UsageIdentity);

                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = StringComparer.Ordinal.Compare(
                    left.FullyQualifiedName,
                    right.FullyQualifiedName);

                return comparison != 0
                    ? comparison
                    : StringComparer.Ordinal.Compare(
                        left.TemplateResultTypeFullyQualifiedName,
                        right.TemplateResultTypeFullyQualifiedName);
            });

        var seen = new HashSet<TemplateDestinationTypeInfo>();
        var result = new List<TemplateDestinationTypeInfo>(
            orderedDestinationTypes.Length);

        foreach (var destinationType in orderedDestinationTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (seen.Add(destinationType))
            {
                result.Add(destinationType);
            }
        }

        return result.ToImmutableArray();
    }

    private readonly record struct TemplateExtensionSignaturePreference
    (
        int DynamicTypeCount,
        int NullableReferenceTypeCount,
        int ExplicitTupleElementNameCount
    )
    {
        public static TemplateExtensionSignaturePreference operator +(
            TemplateExtensionSignaturePreference left,
            TemplateExtensionSignaturePreference right)
        {
            return new TemplateExtensionSignaturePreference(
                left.DynamicTypeCount + right.DynamicTypeCount,
                left.NullableReferenceTypeCount +
                right.NullableReferenceTypeCount,
                left.ExplicitTupleElementNameCount +
                right.ExplicitTupleElementNameCount);
        }
    }

    private static TemplateDestinationTypeKind? GetDestinationTypeKind(
        INamedTypeSymbol destinationType,
        Compilation compilation)
    {
        var templateDestinationType =
            GetTemplateDestinationType(destinationType);

        if (IsNullableValueType(destinationType) &&
            destinationType.TypeArguments[0] is not INamedTypeSymbol ||
            templateDestinationType.IsTupleType ||
            templateDestinationType.IsRefLikeType ||
            IsFileLocal(templateDestinationType) ||
            !compilation.IsSymbolAccessibleWithin(
                destinationType,
                compilation.Assembly))
        {
            return null;
        }

        if (DirectDestinationTypePolicy.IsDirect(destinationType))
        {
            return TemplateDestinationTypeKind.DirectTemplate;
        }

        if (HasDuplicateTypeParameterNames(templateDestinationType))
        {
            return null;
        }

        return templateDestinationType.TypeKind is
            TypeKind.Class or
            TypeKind.Struct or
            TypeKind.Interface
                ? TemplateDestinationTypeKind.GeneratedTemplate
                : null;
    }

    private static bool IsNullableValueType(INamedTypeSymbol type)
    {
        return type.OriginalDefinition.SpecialType ==
               SpecialType.System_Nullable_T;
    }

    private static INamedTypeSymbol GetTemplateDestinationType(
        INamedTypeSymbol destinationType)
    {
        return IsNullableValueType(destinationType) &&
               destinationType.TypeArguments[0] is
                   INamedTypeSymbol underlyingType
            ? underlyingType
            : destinationType;
    }

    private static bool IsNullableDestination(
        INamedTypeSymbol destinationType)
    {
        return IsNullableValueType(destinationType) ||
               destinationType.NullableAnnotation ==
               NullableAnnotation.Annotated;
    }

    private static bool HasDuplicateTypeParameterNames(
        INamedTypeSymbol type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var containingTypes = new Stack<INamedTypeSymbol>();

        for (var current = type.OriginalDefinition;
             current is not null;
             current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        while (containingTypes.Count > 0)
        {
            foreach (var typeParameter in
                     containingTypes.Pop().TypeParameters)
            {
                if (!names.Add(typeParameter.Name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool CanGenerateTemplateExtension(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.TypeParameter)
        {
            return false;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return CanGenerateTemplateExtension(arrayType.ElementType);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return true;
        }

        if (IsFileLocal(namedType))
        {
            return false;
        }

        if (namedType.ContainingType is { } containingType &&
            !CanGenerateTemplateExtension(containingType))
        {
            return false;
        }

        foreach (var typeArgument in namedType.TypeArguments)
        {
            if (!CanGenerateTemplateExtension(typeArgument))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsFileLocal(INamedTypeSymbol type)
    {
        for (var current = type;
             current is not null;
             current = current.ContainingType)
        {
            if (current.IsFileLocal)
            {
                return true;
            }
        }

        return false;
    }
}
