using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperBuilderMap;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TemplateSurface;

internal static class TemplateDestinationTypePipeline
{
    public static IncrementalValuesProvider<TemplateDestinationTypeInfo> Build(
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValueProvider<MappingSettings> assemblySettings,
        IncrementalValuesProvider<MapperBuilderMapInfo> mapInfos)
    {
        var destinationTypes = mapInfos
            .Combine(compilationContext)
            .Combine(assemblySettings)
            .SelectMany(static (source, cancellationToken) =>
            {
                var ((mapInfo, context), settings) = source;

                return BuildDestinationTypes(
                    mapInfo,
                    context,
                    settings,
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
            MappingSettings assemblySettings,
            CancellationToken cancellationToken)
    {
        var semanticModel = context.Compilation.GetSemanticModel(
            mapInfo.ConfigureSyntax.SyntaxTree);

        var result =
            ImmutableArray.CreateBuilder<TemplateDestinationTypeInfo>();

        foreach (var registration in mapInfo.Registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!MappingTypePolicy.IsSupported(
                    registration.SourceType) ||
                !MappingTypePolicy.IsSupported(
                    registration.DestinationType))
            {
                continue;
            }

            var effectiveMode = EffectiveTemplateMode.Resolve(
                assemblySettings,
                mapInfo.Settings,
                registration.Settings);

            if (TryGetDestinationType(
                    registration.Syntax,
                    effectiveMode,
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
        TemplateModeValue? effectiveMode,
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

        var sourceTypeSyntax =
            genericName.TypeArgumentList.Arguments[0];
        var destinationTypeSyntax =
            genericName.TypeArgumentList.Arguments[1];

        var sourceType = semanticModel.GetTypeInfo(
            sourceTypeSyntax,
            cancellationToken).Type;
        var destinationType = semanticModel.GetTypeInfo(
            destinationTypeSyntax,
            cancellationToken).Type;

        if (sourceType is null ||
            destinationType is null)
        {
            return null;
        }

        sourceType = PreserveTopLevelNullableAnnotation(
            sourceTypeSyntax,
            sourceType);

        if (destinationType is IDynamicTypeSymbol)
        {
            destinationType = PreserveTopLevelNullableAnnotation(
                destinationTypeSyntax,
                semanticModel.Compilation.GetSpecialType(
                    SpecialType.System_Object));
        }
        else
        {
            destinationType = PreserveTopLevelNullableAnnotation(
                destinationTypeSyntax,
                destinationType);
        }

        if (destinationType is not INamedTypeSymbol namedDestinationType ||
            !TryGetDestinationTypeKind(
                namedDestinationType,
                effectiveMode,
                semanticModel.Compilation,
                out var kind))
        {
            return null;
        }

        return BuildDestinationTypeInfo(
            sourceType,
            namedDestinationType,
            kind,
            semanticModel.Compilation);
    }

    private static ITypeSymbol PreserveTopLevelNullableAnnotation(
        TypeSyntax syntax,
        ITypeSymbol type)
    {
        return syntax is NullableTypeSyntax &&
               type.IsReferenceType
            ? type.WithNullableAnnotation(
                NullableAnnotation.Annotated)
            : type;
    }

    private static TemplateDestinationTypeInfo BuildDestinationTypeInfo(
        ITypeSymbol sourceType,
        INamedTypeSymbol destinationType,
        TemplateDestinationTypeKind kind,
        Compilation compilation)
    {
        var sourceTypeSignature =
            BuildTemplateExtensionSignature(sourceType);
        var templateExtensionSignature =
            BuildTemplateExtensionSignature(destinationType);

        var sourceTypeFullyQualifiedName = sourceType.ToDisplayString(
            SymbolDisplayFormats.FullyQualifiedNullable);

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

        var canGenerateTemplateExtension =
            CanGenerateTopLevelTypeReference(destinationType);

        var canGeneratePairSpecificTemplateExtension =
            canGenerateTemplateExtension &&
            CanGenerateTopLevelTypeReference(sourceType) &&
            IsTypeAccessibleWithin(
                sourceType,
                compilation,
                compilation.Assembly);

        if (kind != TemplateDestinationTypeKind.GeneratedTemplate)
        {
            return new TemplateDestinationTypeInfo(
                kind,
                null,
                sourceTypeSignature,
                templateExtensionSignature,
                sourceTypeFullyQualifiedName,
                usageIdentity,
                fullyQualifiedName,
                existingDestinationTypeFullyQualifiedName,
                fullyQualifiedName,
                canGenerateTemplateExtension,
                canGeneratePairSpecificTemplateExtension);
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
            sourceTypeSignature,
            templateExtensionSignature,
            sourceTypeFullyQualifiedName,
            usageIdentity,
            fullyQualifiedName,
            existingDestinationTypeFullyQualifiedName,
            templateTypeFullyQualifiedName,
            canGenerateTemplateExtension,
            canGeneratePairSpecificTemplateExtension);
    }

    private static TemplateExtensionSignatureInfo
        BuildTemplateExtensionSignature(ITypeSymbol type)
    {
        var preference = GetTemplateExtensionSignaturePreference(
            type);

        return new TemplateExtensionSignatureInfo(
            DocumentationCommentId.CreateReferenceId(type) ??
            type.ToDisplayString(
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
        var groups =
            new Dictionary<
                string,
                List<TemplateDestinationTypeInfo>>(
                StringComparer.Ordinal);

        foreach (var destinationType in destinationTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var identity =
                destinationType.TemplateExtensionSignature.Identity;

            if (!groups.TryGetValue(identity, out var group))
            {
                group = new List<TemplateDestinationTypeInfo>();
                groups.Add(identity, group);
            }

            group.Add(destinationType);
        }

        var coordinatedDestinationTypes =
            new List<TemplateDestinationTypeInfo>(
                destinationTypes.Length);

        foreach (var group in groups.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var firstKind = group[0].Kind;

            if (group.All(destinationType =>
                    destinationType.Kind == firstKind))
            {
                coordinatedDestinationTypes.Add(
                    RemoveSourceSpecificDetails(
                        SelectCanonicalDestination(group)));
                continue;
            }

            var seen = new HashSet<TemplateDestinationTypeInfo>();

            foreach (var destinationType in group)
            {
                if (seen.Add(destinationType))
                {
                    coordinatedDestinationTypes.Add(destinationType);
                }
            }
        }

        var orderedDestinationTypes =
            coordinatedDestinationTypes.ToArray();

        Array.Sort(
            orderedDestinationTypes,
            static (left, right) =>
            {
                var comparison = StringComparer.Ordinal.Compare(
                    left.TemplateExtensionSignature.Identity,
                    right.TemplateExtensionSignature.Identity);

                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = StringComparer.Ordinal.Compare(
                    left.SourceTypeSignature.Identity,
                    right.SourceTypeSignature.Identity);

                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = left.Kind.CompareTo(right.Kind);

                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = StringComparer.Ordinal.Compare(
                    left.UsageIdentity,
                    right.UsageIdentity);

                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = StringComparer.Ordinal.Compare(
                    left.SourceTypeFullyQualifiedName,
                    right.SourceTypeFullyQualifiedName);

                return comparison != 0
                    ? comparison
                    : StringComparer.Ordinal.Compare(
                        left.FullyQualifiedName,
                        right.FullyQualifiedName);
            });

        return orderedDestinationTypes.ToImmutableArray();
    }

    private static TemplateDestinationTypeInfo
        SelectCanonicalDestination(
            IReadOnlyList<TemplateDestinationTypeInfo> destinationTypes)
    {
        var canonical = destinationTypes[0];

        for (var index = 1;
             index < destinationTypes.Count;
             index++)
        {
            var candidate = destinationTypes[index];

            if (CompareCanonicalPreference(
                    candidate,
                    canonical) < 0)
            {
                canonical = candidate;
            }
        }

        return canonical;
    }

    private static int CompareCanonicalPreference(
        TemplateDestinationTypeInfo left,
        TemplateDestinationTypeInfo right)
    {
        var leftSignature = left.TemplateExtensionSignature;
        var rightSignature = right.TemplateExtensionSignature;

        var comparison = leftSignature.DynamicTypeCount.CompareTo(
            rightSignature.DynamicTypeCount);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = leftSignature.NullableReferenceTypeCount.CompareTo(
            rightSignature.NullableReferenceTypeCount);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = leftSignature.ExplicitTupleElementNameCount.CompareTo(
            rightSignature.ExplicitTupleElementNameCount);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = StringComparer.Ordinal.Compare(
            left.UsageIdentity,
            right.UsageIdentity);

        return comparison != 0
            ? comparison
            : StringComparer.Ordinal.Compare(
                left.FullyQualifiedName,
                right.FullyQualifiedName);
    }

    private static TemplateDestinationTypeInfo
        RemoveSourceSpecificDetails(
            TemplateDestinationTypeInfo destinationType)
    {
        return destinationType with
        {
            SourceTypeSignature = default,
            SourceTypeFullyQualifiedName = string.Empty,
            CanGeneratePairSpecificTemplateExtension = false
        };
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

    private static bool TryGetDestinationTypeKind(
        INamedTypeSymbol destinationType,
        TemplateModeValue? effectiveMode,
        Compilation compilation,
        out TemplateDestinationTypeKind kind)
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
            kind = default;
            return false;
        }

        if (effectiveMode is null or
            TemplateModeValue.Default)
        {
            kind = TemplateDestinationTypeKind.None;
            return true;
        }

        if (effectiveMode == TemplateModeValue.Raw)
        {
            kind = IsDirectTemplateSupported(templateDestinationType)
                ? TemplateDestinationTypeKind.DirectTemplate
                : TemplateDestinationTypeKind.None;
            return true;
        }

        if (DirectDestinationTypePolicy.IsDirect(destinationType))
        {
            kind = TemplateDestinationTypeKind.DirectTemplate;
            return true;
        }

        if (HasDuplicateTypeParameterNames(templateDestinationType))
        {
            kind = TemplateDestinationTypeKind.None;
            return true;
        }

        kind = templateDestinationType.TypeKind is
            TypeKind.Class or
            TypeKind.Struct or
            TypeKind.Interface
                ? TemplateDestinationTypeKind.GeneratedTemplate
                : TemplateDestinationTypeKind.None;
        return true;
    }

    private static bool IsDirectTemplateSupported(
        INamedTypeSymbol destinationType)
    {
        return DirectDestinationTypePolicy.IsDirect(destinationType) ||
               destinationType.TypeKind is
                   TypeKind.Class or
                   TypeKind.Struct or
                   TypeKind.Interface or
                   TypeKind.Enum;
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

    private static bool CanGenerateTopLevelTypeReference(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.TypeParameter)
        {
            return false;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return CanGenerateTopLevelTypeReference(
                arrayType.ElementType);
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
            !CanGenerateTopLevelTypeReference(containingType))
        {
            return false;
        }

        foreach (var typeArgument in namedType.TypeArguments)
        {
            if (!CanGenerateTopLevelTypeReference(typeArgument))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTypeAccessibleWithin(
        ITypeSymbol type,
        Compilation compilation,
        ISymbol within)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            return IsTypeAccessibleWithin(
                arrayType.ElementType,
                compilation,
                within);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return type is IDynamicTypeSymbol;
        }

        if (!compilation.IsSymbolAccessibleWithin(
                namedType,
                within))
        {
            return false;
        }

        foreach (var typeArgument in namedType.TypeArguments)
        {
            if (!IsTypeAccessibleWithin(
                    typeArgument,
                    compilation,
                    within))
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
