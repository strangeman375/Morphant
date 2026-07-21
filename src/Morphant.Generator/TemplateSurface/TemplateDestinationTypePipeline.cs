using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.TemplateSurface;

internal static class TemplateDestinationTypePipeline
{
    public static IncrementalValuesProvider<TemplateDestinationTypeInfo> Build(
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValuesProvider<TypeMapperConfigureInfo> configureInfos)
    {
        var destinationTypes = configureInfos
            .Combine(compilationContext)
            .SelectMany(static (source, cancellationToken) =>
            {
                var (configureInfo, context) = source;

                return BuildDestinationTypes(
                    configureInfo,
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
            TypeMapperConfigureInfo configureInfo,
            CompilationContext context,
            CancellationToken cancellationToken)
    {
        if (context.KnownSymbols is not { } knownSymbols)
        {
            return ImmutableArray<TemplateDestinationTypeInfo>.Empty;
        }

        var semanticModel = context.Compilation.GetSemanticModel(
            configureInfo.Syntax.SyntaxTree);

        var result =
            ImmutableArray.CreateBuilder<TemplateDestinationTypeInfo>();

        foreach (var invocation in configureInfo.Syntax
                     .DescendantNodes()
                     .OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsMapInvocationCandidate(invocation))
            {
                continue;
            }

            if (TryGetDestinationType(
                    invocation,
                    semanticModel,
                    knownSymbols,
                    cancellationToken) is { } destinationType)
            {
                result.Add(destinationType);
            }
        }

        return result.ToImmutable();
    }

    private static bool IsMapInvocationCandidate(
        InvocationExpressionSyntax invocation)
    {
        return invocation is
        {
            ArgumentList.Arguments.Count: <= 1,
            Expression: MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax
                {
                    Identifier.ValueText: "Map",
                    TypeArgumentList.Arguments.Count: 2
                }
            }
        };
    }

    private static TemplateDestinationTypeInfo? TryGetDestinationType(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(
            invocation,
            cancellationToken);

        if (symbolInfo.Symbol is not IMethodSymbol method ||
            !IsMapperBuilderMapMethod(method, knownSymbols))
        {
            return null;
        }

        var destinationType = method.TypeArguments[1];

        if (destinationType is IDynamicTypeSymbol)
        {
            return BuildDestinationTypeInfo(
                semanticModel.Compilation.GetSpecialType(
                    SpecialType.System_Object),
                TemplateDestinationTypeKind.DirectTemplate);
        }

        if (destinationType is not INamedTypeSymbol namedDestinationType ||
            GetDestinationTypeKind(
                namedDestinationType,
                semanticModel.Compilation) is not { } kind)
        {
            return null;
        }

        return BuildDestinationTypeInfo(
            namedDestinationType,
            kind);
    }

    private static TemplateDestinationTypeInfo BuildDestinationTypeInfo(
        INamedTypeSymbol destinationType,
        TemplateDestinationTypeKind kind)
    {
        var definition = destinationType.OriginalDefinition;

        var definitionMetadataName =
            SymbolNameHelper.GetFullMetadataName(definition);

        var fullyQualifiedName = destinationType.ToDisplayString(
            SymbolDisplayFormats.FullyQualifiedNullable);

        var templateTypeArgumentList =
            BuildTemplateTypeArgumentList(destinationType);

        var usageIdentity =
            definitionMetadataName + templateTypeArgumentList;

        if (kind == TemplateDestinationTypeKind.DirectTemplate)
        {
            return new TemplateDestinationTypeInfo(
                kind,
                null,
                usageIdentity,
                fullyQualifiedName,
                fullyQualifiedName);
        }

        var templateNamespace =
            BuildTemplateNamespace(definition);

        var templateTypeName =
            definition.Name + "MorphantTemplate";

        var templateTypeFullyQualifiedName =
            "global::" +
            templateNamespace +
            "." +
            templateTypeName +
            templateTypeArgumentList;

        return new TemplateDestinationTypeInfo(
            kind,
            new TemplateTypeDefinitionInfo(
                definitionMetadataName,
                templateNamespace,
                templateTypeName),
            usageIdentity,
            fullyQualifiedName,
            templateTypeFullyQualifiedName);
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
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<TemplateDestinationTypeInfo>();

        foreach (var destinationType in destinationTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (seen.Add(destinationType.UsageIdentity))
            {
                result.Add(destinationType);
            }
        }

        result.Sort(static (left, right) =>
            StringComparer.Ordinal.Compare(
                left.UsageIdentity,
                right.UsageIdentity));

        return result.ToImmutableArray();
    }

    private static bool IsMapperBuilderMapMethod(
        IMethodSymbol method,
        KnownSymbols knownSymbols)
    {
        return method.Name == "Map" &&
               method.MethodKind == MethodKind.Ordinary &&
               !method.IsStatic &&
               method.Parameters.Length == 1 &&
               method.TypeArguments.Length == 2 &&
               SymbolEqualityComparer.Default.Equals(
                   method.ContainingType,
                   knownSymbols.MapperBuilder);
    }

    private static TemplateDestinationTypeKind? GetDestinationTypeKind(
        INamedTypeSymbol destinationType,
        Compilation compilation)
    {
        if (ContainsTypeParameter(destinationType) ||
            destinationType.IsTupleType ||
            destinationType.IsRefLikeType ||
            IsFileLocal(destinationType) ||
            !compilation.IsSymbolAccessibleWithin(
                destinationType,
                compilation.Assembly))
        {
            return null;
        }

        if (IsDirectTemplateDestination(destinationType))
        {
            return TemplateDestinationTypeKind.DirectTemplate;
        }

        if (IsNullableValueType(destinationType) ||
            HasDuplicateTypeParameterNames(destinationType))
        {
            return null;
        }

        return destinationType.TypeKind is
            TypeKind.Class or
            TypeKind.Struct or
            TypeKind.Interface
                ? TemplateDestinationTypeKind.GeneratedTemplate
                : null;
    }

    private static bool IsDirectTemplateDestination(
        INamedTypeSymbol destinationType)
    {
        if (IsCSharpPredefinedType(destinationType) ||
            destinationType.TypeKind == TypeKind.Enum ||
            IsSupportedBclDirectTemplateType(destinationType))
        {
            return true;
        }

        return IsNullableValueType(destinationType) &&
               destinationType.TypeArguments[0] is
                   INamedTypeSymbol underlyingType &&
               IsDirectTemplateDestination(underlyingType);
    }

    private static bool IsCSharpPredefinedType(INamedTypeSymbol type)
    {
        return type.SpecialType is
            SpecialType.System_Object or
            SpecialType.System_String or
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
            SpecialType.System_Decimal;
    }

    private static bool IsNullableValueType(INamedTypeSymbol type)
    {
        return type.OriginalDefinition.SpecialType ==
               SpecialType.System_Nullable_T;
    }

    private static bool IsSupportedBclDirectTemplateType(
        INamedTypeSymbol type)
    {
        return SymbolNameHelper.GetFullMetadataName(type.OriginalDefinition) is
            "System.Guid" or
            "System.DateTime" or
            "System.DateTimeOffset" or
            "System.DateOnly" or
            "System.TimeOnly" or
            "System.TimeSpan" or
            "System.Half" or
            "System.Int128" or
            "System.UInt128" or
            "System.Uri" or
            "System.Version" or
            "System.Numerics.BigInteger" or
            "System.Numerics.Complex" or
            "System.Text.Rune" or
            "System.Index" or
            "System.Range";
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

    private static bool ContainsTypeParameter(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.TypeParameter)
        {
            return true;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return ContainsTypeParameter(arrayType.ElementType);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (namedType.ContainingType is not null &&
            ContainsTypeParameter(namedType.ContainingType))
        {
            return true;
        }

        foreach (var typeArgument in namedType.TypeArguments)
        {
            if (ContainsTypeParameter(typeArgument))
            {
                return true;
            }
        }

        return false;
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
