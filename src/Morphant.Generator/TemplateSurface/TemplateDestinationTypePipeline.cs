using System.Collections.Immutable;
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

        var destinationType = method.TypeArguments[1]
            .WithNullableAnnotation(
                NullableAnnotation.NotAnnotated);

        if (destinationType is not INamedTypeSymbol namedDestinationType ||
            !IsSupportedDestinationType(namedDestinationType))
        {
            return null;
        }

        return BuildDestinationTypeInfo(
            namedDestinationType);
    }

    private static TemplateDestinationTypeInfo BuildDestinationTypeInfo(
        INamedTypeSymbol destinationType)
    {
        var metadataName =
            SymbolNameHelper.GetFullMetadataName(destinationType);

        var fullyQualifiedName = destinationType.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat);

        var templateNamespace =
            BuildTemplateNamespace(destinationType);

        var templateTypeName =
            destinationType.Name + "MorphantTemplate";

        var templateTypeFullyQualifiedName =
            "global::" +
            templateNamespace +
            "." +
            templateTypeName;

        return new TemplateDestinationTypeInfo(
            metadataName,
            fullyQualifiedName,
            templateNamespace,
            templateTypeName,
            templateTypeFullyQualifiedName);
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
            containingTypeScopes.Push(
                containingType.Name + "Scope");
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

            if (seen.Add(destinationType.MetadataName))
            {
                result.Add(destinationType);
            }
        }

        result.Sort(static (left, right) =>
            StringComparer.Ordinal.Compare(
                left.MetadataName,
                right.MetadataName));

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

    private static bool IsSupportedDestinationType(
        INamedTypeSymbol destinationType)
    {
        if (destinationType.IsGenericType ||
            destinationType.IsTupleType)
        {
            return false;
        }

        return destinationType.TypeKind is
            TypeKind.Class or
            TypeKind.Struct or
            TypeKind.Interface;
    }
}
