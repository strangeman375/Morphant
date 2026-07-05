using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.TemplateSurface.TemplateExtension;
using Morphant.Generator.TemplateSurface.TemplateType;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.TemplateSurface;

internal static class TemplateSurfacePipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValuesProvider<TypeMapperConfigureInfo> configureInfos)
    {
        var mapUsages = configureInfos
            .Combine(compilationContext)
            .SelectMany(static (x, cancellationToken) =>
            {
                var (configureInfo, compilationContext) = x;

                return BuildMapUsages(
                    configureInfo,
                    compilationContext,
                    cancellationToken);
            })
            .WithTrackingName(MorphantGeneratorStageNames.BuildMapperBuilderMapInfos);

        TemplateTypePipeline.Register(context, compilationContext, mapUsages);
        TemplateExtensionPipeline.Register(context, compilationContext, mapUsages);
    }

    private static bool IsMapInvocationCandidate(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax
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

    private static ImmutableArray<MapperBuilderMapInfo> BuildMapUsages(
        TypeMapperConfigureInfo configureInfo,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        var invocations = configureInfo.Syntax
            .DescendantNodes()
            .Where(IsMapInvocationCandidate);

        var semanticModel = context.Compilation.GetSemanticModel(configureInfo.Syntax.SyntaxTree);

        var builder = ImmutableArray.CreateBuilder<MapperBuilderMapInfo>();
        foreach (var invocation in invocations)
        {
            if (TryBuildMapUsage(
                    (InvocationExpressionSyntax)invocation,
                    semanticModel,
                    context.KnownSymbols,
                    cancellationToken) is { } usage)
            {
                builder.Add(usage);
            }
        }

        return builder.ToImmutable();
    }

    private static MapperBuilderMapInfo? TryBuildMapUsage(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(
            invocation,
            cancellationToken);

        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return null;
        }

        if (!IsMapperBuilderMapMethod(method, knownSymbols))
        {
            return null;
        }

        var sourceType = method.TypeArguments[0];
        var destinationType = NormalizeDestinationType(method.TypeArguments[1]);

        if (destinationType is not INamedTypeSymbol namedDestinationType
            || !IsSupportedDestinationType(destinationType))
        {
            return null;
        }

        var sourceTypeName = sourceType.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat);

        var destinationTypeReference = BuildDestinationTypeReference(namedDestinationType);

        return new MapperBuilderMapInfo(
            sourceTypeName,
            destinationTypeReference);
    }

    private static DestinationTypeReference BuildDestinationTypeReference(
        INamedTypeSymbol destinationType)
    {
        var metadataName = SymbolNameHelper.GetFullMetadataName(destinationType);

        var fullyQualifiedName = destinationType.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat);

        var destinationNamespace = destinationType.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : destinationType.ContainingNamespace.ToDisplayString();

        var templateNamespace = string.IsNullOrEmpty(destinationNamespace)
            ? "Morphant.Generated"
            : destinationNamespace + ".Morphant.Generated";

        var templateTypeName = destinationType.Name + "MorphantTemplate";

        var templateTypeFullyQualifiedName =
            "global::" + templateNamespace + "." + templateTypeName;

        return new DestinationTypeReference(
            metadataName,
            fullyQualifiedName,
            templateNamespace,
            templateTypeName,
            templateTypeFullyQualifiedName);
    }

    private static bool IsMapperBuilderMapMethod(
        IMethodSymbol method,
        KnownSymbols knownSymbols)
    {
        return method.Name == "Map"
               && method.MethodKind == MethodKind.Ordinary
               && !method.IsStatic
               && method.Parameters.Length == 1
               && method.TypeArguments.Length == 2
               && SymbolEqualityComparer.Default.Equals(method.ContainingType, knownSymbols.MapperBuilder);
    }

    private static ITypeSymbol NormalizeDestinationType(ITypeSymbol destinationType) =>
        destinationType.WithNullableAnnotation(NullableAnnotation.NotAnnotated);

    private static bool IsSupportedDestinationType(ITypeSymbol destinationType)
    {
        if (destinationType is not INamedTypeSymbol namedType)
        {
            return false;
        }

        // Пока не поддерживаем generic destination types:
        // ApiResponse<UserModel>, List<UserModel>, etc.
        if (namedType.IsGenericType)
        {
            return false;
        }

        // Пока не поддерживаем tuple destination types.
        if (namedType.IsTupleType)
        {
            return false;
        }

        // Пока не поддерживаем массивы и прочие спец. формы.
        // Массивы обычно не INamedTypeSymbol, но оставляем намерение явно.
        return destinationType.TypeKind is TypeKind.Class
            or TypeKind.Struct
            or TypeKind.Interface;
    }

    private static string GetContainingNamespace(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var typeDeclaration = node
            .Ancestors()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();

        if (typeDeclaration is null)
        {
            return string.Empty;
        }

        var typeSymbol = semanticModel.GetDeclaredSymbol(
            typeDeclaration,
            cancellationToken);

        var namespaceSymbol = typeSymbol?.ContainingNamespace;

        if (namespaceSymbol is null || namespaceSymbol.IsGlobalNamespace)
        {
            return string.Empty;
        }

        return namespaceSymbol.ToDisplayString();
    }
}
