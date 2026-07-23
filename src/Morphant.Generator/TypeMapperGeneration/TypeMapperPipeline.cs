using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperBuilderMap;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TypeMapperPipeline
{
    private const string SetsRequiredMembersAttributeMetadataName =
        "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";

    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValuesProvider<MapperBuilderMapInfo> mapInfos)
    {
        var requests = mapInfos
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                TryBuildGenerationInput(source, cancellationToken))
            .WhereHasValue()
            .Collect()
            .SelectMany(static (generationInputs, cancellationToken) =>
                BuildRequests(
                    generationInputs,
                    cancellationToken))
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildTypeMappers);

        context.RegisterSourceOutput(
            requests,
            static (context, request) =>
                context.AddSource(
                    request.HintName,
                    TypeMapperEmitter.Emit(request.Model)));
    }

    private static TypeMapperGenerationInput? TryBuildGenerationInput(
        (
            MapperBuilderMapInfo MapInfo,
            CompilationContext Context
        ) source,
        CancellationToken cancellationToken)
    {
        var (mapInfo, context) = source;

        var semanticModel = context.Compilation.GetSemanticModel(
            mapInfo.ConfigureSyntax.SyntaxTree);

        if (mapInfo.ConfigureSyntax.Parent is not ClassDeclarationSyntax mapperDeclaration ||
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
            mapInfo,
            context.Compilation,
            mapperType,
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

        return new TypeMapperGenerationInput(
            SymbolNameHelper.GetFullMetadataName(mapperType),
            model);
    }

    private static ImmutableArray<TypeMapperRequest> BuildRequests(
        ImmutableArray<TypeMapperGenerationInput> generationInputs,
        CancellationToken cancellationToken)
    {
        var orderedInputs = generationInputs.ToArray();

        Array.Sort(
            orderedInputs,
            static (left, right) =>
                StringComparer.Ordinal.Compare(
                    left.StableIdentity,
                    right.StableIdentity));

        var hintNamePartAllocator = new HintNamePartAllocator();
        var requests =
            ImmutableArray.CreateBuilder<TypeMapperRequest>(
                orderedInputs.Length);

        foreach (var generationInput in orderedInputs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hintName = GeneratedSourceHintName.Create(
                "TypeMapper",
                hintNamePartAllocator.Allocate(
                    generationInput.StableIdentity));

            requests.Add(
                new TypeMapperRequest(
                    hintName,
                    generationInput.Model));
        }

        return requests.ToImmutable();
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
        MapperBuilderMapInfo mapInfo,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var registrations =
            ImmutableArray.CreateBuilder<
                MapperBuilderMapRegistrationInfo>();

        foreach (var registration in mapInfo.Registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!MappingTypePolicy.IsSupported(
                    registration.SourceType) ||
                !MappingTypePolicy.IsSupported(
                    registration.DestinationType) ||
                registrations.Any(
                    existing =>
                        TypeMapperMappingTypePolicy.AreEquivalent(
                            existing.SourceType,
                            registration.SourceType) &&
                        TypeMapperMappingTypePolicy.AreEquivalent(
                            existing.DestinationType,
                            registration.DestinationType)))
            {
                continue;
            }

            registrations.Add(registration);
        }

        for (var leftIndex = 0;
             leftIndex < registrations.Count;
             leftIndex++)
        {
            for (var rightIndex = leftIndex + 1;
                 rightIndex < registrations.Count;
                 rightIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var left = registrations[leftIndex];
                var right = registrations[rightIndex];

                if (TypeMapperMappingTypePolicy.CanMappingsUnify(
                        left.SourceType,
                        left.DestinationType,
                        right.SourceType,
                        right.DestinationType))
                {
                    return default;
                }
            }
        }

        return registrations
            .Select(registration =>
                BuildMapping(
                    registration,
                    compilation,
                    mapperType,
                    cancellationToken))
            .ToImmutableArray();
    }

    private static TypeMapperMappingModel BuildMapping(
        MapperBuilderMapRegistrationInfo registration,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var destination =
            GetSupportedClassDestination(
                registration.DestinationType);

        var memberMappings = ConventionMemberMappingPlanner.Build(
            registration.SourceType,
            destination,
            compilation,
            mapperType,
            cancellationToken);

        return new TypeMapperMappingModel(
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                registration.SourceType),
            TypeMapperMappingTypePolicy
                .GetGeneratedMaybeNullTypeName(
                    registration.SourceType),
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                registration.DestinationType),
            TypeMapperMappingTypePolicy
                .GetGeneratedMaybeNullTypeName(
                    registration.DestinationType),
            CanMapNewWithParameterlessConstructor(
                destination,
                compilation,
                mapperType,
                memberMappings.HasUnmappedRequiredMembers),
            destination is not null,
            memberMappings.MapNew,
            memberMappings.MapExisting);
    }

    private static bool CanMapNewWithParameterlessConstructor(
        INamedTypeSymbol? destination,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        bool hasUnmappedRequiredMembers)
    {
        if (destination is null ||
            destination.IsAbstract)
        {
            return false;
        }

        var parameterlessConstructor =
            destination.InstanceConstructors.FirstOrDefault(
                static constructor =>
                    constructor.Parameters.IsEmpty);

        return parameterlessConstructor is not null &&
               compilation.IsSymbolAccessibleWithin(
                   parameterlessConstructor,
                   mapperType) &&
               (!hasUnmappedRequiredMembers ||
                HasSetsRequiredMembersAttribute(
                    parameterlessConstructor));
    }

    private static bool HasSetsRequiredMembersAttribute(
        IMethodSymbol constructor)
    {
        foreach (var attribute in constructor.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeType &&
                SymbolNameHelper.GetFullMetadataName(
                    attributeType) ==
                SetsRequiredMembersAttributeMetadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static INamedTypeSymbol? GetSupportedClassDestination(
        ITypeSymbol destinationType)
    {
        if (destinationType is not INamedTypeSymbol
            {
                TypeKind: TypeKind.Class,
                IsRecord: false,
                SpecialType: SpecialType.None
            } destination ||
            destination.NullableAnnotation ==
                NullableAnnotation.Annotated)
        {
            return null;
        }

        return destination;
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

    private readonly record struct TypeMapperGenerationInput(
        string StableIdentity,
        TypeMapperModel Model);
}
