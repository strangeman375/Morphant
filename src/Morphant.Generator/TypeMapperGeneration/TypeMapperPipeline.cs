using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TypeMapperPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValuesProvider<TypeMapperConfigureInfo> configureInfos)
    {
        var requests = configureInfos
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                TryBuildRequest(source, cancellationToken))
            .WhereHasValue()
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildTypeMappers);

        context.RegisterSourceOutput(
            requests,
            static (context, request) =>
                context.AddSource(
                    request.HintName,
                    TypeMapperEmitter.Emit(request.Model)));
    }

    private static TypeMapperRequest? TryBuildRequest(
        (
            TypeMapperConfigureInfo ConfigureInfo,
            CompilationContext Context
        ) source,
        CancellationToken cancellationToken)
    {
        var (configureInfo, context) = source;

        if (context.KnownSymbols is not { } knownSymbols)
        {
            return null;
        }

        var semanticModel = context.Compilation.GetSemanticModel(
            configureInfo.Syntax.SyntaxTree);

        if (configureInfo.Syntax.Parent is not ClassDeclarationSyntax mapperDeclaration ||
            semanticModel.GetDeclaredSymbol(
                mapperDeclaration,
                cancellationToken) is not INamedTypeSymbol mapperType ||
            !CanGenerate(
                mapperType,
                mapperDeclaration))
        {
            return null;
        }

        var mappings = BuildMappings(
            configureInfo,
            semanticModel,
            knownSymbols,
            cancellationToken);

        if (mappings.IsDefaultOrEmpty)
        {
            return null;
        }

        var mapperNamespace =
            mapperType.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : mapperType.ContainingNamespace.ToDisplayString();

        var model = new TypeMapperModel(
            mapperNamespace,
            BuildContainingTypes(mapperDeclaration),
            GetAccessibility(mapperType.DeclaredAccessibility),
            mapperDeclaration.Identifier.Text,
            BuildTypeParameterList(
                mapperDeclaration.TypeParameterList),
            mappings);

        var hintName = GeneratedSourceHintName.Create(
            "TypeMapper",
            HintNameHelper.ToHintNamePart(
                SymbolNameHelper.GetFullMetadataName(mapperType)));

        return new TypeMapperRequest(
            hintName,
            model);
    }

    private static bool CanGenerate(
        INamedTypeSymbol mapperType,
        ClassDeclarationSyntax mapperDeclaration)
    {
        if (!IsPartial(mapperDeclaration) ||
            !IsSupportedAccessibility(
                mapperType.DeclaredAccessibility) ||
            mapperDeclaration
                .Ancestors()
                .OfType<TypeDeclarationSyntax>()
                .Any(static declaration =>
                    !IsPartial(declaration)))
        {
            return false;
        }

        for (var current = mapperType;
             current is not null;
             current = current.ContainingType)
        {
            if (current.IsFileLocal)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPartial(
        TypeDeclarationSyntax declaration)
    {
        return declaration.Modifiers.Any(
            SyntaxKind.PartialKeyword);
    }

    private static bool IsSupportedAccessibility(
        Accessibility accessibility)
    {
        return accessibility is
            Accessibility.Public or
            Accessibility.Internal or
            Accessibility.Private or
            Accessibility.Protected or
            Accessibility.ProtectedAndInternal or
            Accessibility.ProtectedOrInternal;
    }

    private static ImmutableArray<TypeMapperContainingTypeModel>
        BuildContainingTypes(
            ClassDeclarationSyntax mapperDeclaration)
    {
        return mapperDeclaration
            .Ancestors()
            .OfType<TypeDeclarationSyntax>()
            .Reverse()
            .Select(static declaration =>
                new TypeMapperContainingTypeModel(
                    GetDeclarationKind(declaration),
                    declaration.Identifier.Text,
                    BuildTypeParameterList(
                        declaration.TypeParameterList)))
            .ToImmutableArray();
    }

    private static string GetDeclarationKind(
        TypeDeclarationSyntax declaration)
    {
        if (declaration is RecordDeclarationSyntax recordDeclaration)
        {
            return recordDeclaration.ClassOrStructKeyword.IsKind(
                SyntaxKind.StructKeyword)
                    ? "record struct"
                    : "record";
        }

        return declaration switch
        {
            ClassDeclarationSyntax => "class",
            StructDeclarationSyntax => "struct",
            InterfaceDeclarationSyntax => "interface",
            _ => throw new InvalidOperationException(
                $"Unsupported containing type declaration: {declaration.Kind()}.")
        };
    }

    private static string BuildTypeParameterList(
        TypeParameterListSyntax? typeParameterList)
    {
        if (typeParameterList is null)
        {
            return string.Empty;
        }

        return
            "<" +
            string.Join(
                ", ",
                typeParameterList.Parameters.Select(
                    static parameter =>
                        parameter.Identifier.Text)) +
            ">";
    }

    private static ImmutableArray<TypeMapperMappingModel> BuildMappings(
        TypeMapperConfigureInfo configureInfo,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        var mappings =
            ImmutableArray.CreateBuilder<TypeMapperMappingModel>();
        var seen = new HashSet<TypeMapperMappingModel>();

        foreach (var invocation in configureInfo.Syntax
                     .DescendantNodes()
                     .OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsMapInvocationCandidate(invocation) ||
                semanticModel.GetSymbolInfo(
                    invocation,
                    cancellationToken).Symbol is not IMethodSymbol method ||
                !IsMapperBuilderMapMethod(method, knownSymbols))
            {
                continue;
            }

            var sourceType = method.TypeArguments[0];
            var destinationType = method.TypeArguments[1];

            if (ContainsFileLocalType(sourceType) ||
                ContainsFileLocalType(destinationType))
            {
                continue;
            }

            var mapping = new TypeMapperMappingModel(
                sourceType.ToDisplayString(
                    SymbolDisplayFormats.FullyQualifiedNullable),
                destinationType.ToDisplayString(
                    SymbolDisplayFormats.FullyQualifiedNullable));

            if (seen.Add(mapping))
            {
                mappings.Add(mapping);
            }
        }

        return mappings.ToImmutable();
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

    private static bool ContainsFileLocalType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            return ContainsFileLocalType(arrayType.ElementType);
        }

        if (type is IPointerTypeSymbol pointerType)
        {
            return ContainsFileLocalType(pointerType.PointedAtType);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        for (var current = namedType;
             current is not null;
             current = current.ContainingType)
        {
            if (current.IsFileLocal)
            {
                return true;
            }
        }

        return namedType.TypeArguments.Any(ContainsFileLocalType);
    }

    private static string GetAccessibility(
        Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal =>
                "private protected",
            Accessibility.ProtectedOrInternal =>
                "protected internal",
            _ => throw new InvalidOperationException(
                $"Unsupported mapper accessibility: {accessibility}.")
        };
    }
}
