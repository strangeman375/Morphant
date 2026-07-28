using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperBuilderMap;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TypeMapperPipeline
{
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
        var destinationPlan =
            BuildDestinationPlan(
                registration.DestinationType,
                cancellationToken);

        var conventionMemberMappings = ConventionMemberMappingPlanner.Build(
            registration.SourceType,
            destinationPlan.MemberType,
            compilation,
            mapperType,
            cancellationToken);
        var templateMapping = TemplateMappingPlanner.Build(
            registration,
            destinationPlan.MemberType,
            compilation,
            mapperType,
            cancellationToken);
        var memberMappings = MergeMemberMappings(
            conventionMemberMappings,
            templateMapping,
            destinationPlan.MemberType,
            cancellationToken);
        var factoryMapNewMemberMappings =
            BuildFactoryMapNewMemberMappings(
                memberMappings,
                templateMapping);
        var factoryMapping = BuildFactoryMapping(
            destinationPlan,
            templateMapping,
            factoryMapNewMemberMappings,
            mapperType);
        var constructorMapping = BuildConstructorMapping(
            registration.SourceType,
            destinationPlan.MemberType,
            memberMappings,
            templateMapping,
            compilation,
            mapperType,
            cancellationToken);
        var mapExistingDestinationLocalName =
            destinationPlan.MapExistingKind ==
                TypeMapperMapExistingKind.NullableValue &&
            !memberMappings.MapExisting.IsEmpty
                ? AllocateDestinationValueLocalName(mapperType)
                : null;
        var mapExistingMemberMappings =
            BuildMapExistingMemberMappings(
                memberMappings.MapExisting,
                templateMapping,
                mapperType,
                mapExistingDestinationLocalName);

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
            templateMapping?.MapNewDirectExpression,
            templateMapping?.MapExistingDirectExpression,
            factoryMapping,
            constructorMapping?.Constructor,
            destinationPlan.MapExistingKind,
            mapExistingDestinationLocalName,
            factoryMapping is not null
                ? factoryMapNewMemberMappings
                : constructorMapping?.MapNewMemberMappings ??
                  memberMappings.MapNew,
            mapExistingMemberMappings);
    }

    private static ImmutableArray<TypeMapperMemberMappingModel>
        BuildFactoryMapNewMemberMappings(
            ConventionMemberMappingPlan memberMappings,
            TemplateMappingPlan? template)
    {
        if (template is not
            {
                ConstructionKind:
                    TemplateConstructionKind.ByFactory
            })
        {
            return [];
        }

        var assignableMemberNames =
            new HashSet<string>(
                memberMappings.MapExisting.Select(
                    static mapping =>
                        mapping.DestinationMemberName),
                StringComparer.Ordinal);

        return memberMappings.MapNew
            .Where(mapping =>
                assignableMemberNames.Contains(
                    mapping.DestinationMemberName))
            .ToImmutableArray();
    }

    private static ImmutableArray<TypeMapperMemberMappingModel>
        BuildMapExistingMemberMappings(
            ImmutableArray<TypeMapperMemberMappingModel> mappings,
            TemplateMappingPlan? template,
            INamedTypeSymbol mapperType,
            string? destinationLocalName)
    {
        if (template is not
            {
                HasDestinationParameter: true,
                MapExistingDirectExpression: null
            })
        {
            return mappings;
        }

        var result = mappings.ToArray();
        var usedNames =
            ConventionConstructorMappingPlanner
                .BuildUsedValueLocalNames(mapperType);

        usedNames.Add("destination");

        if (destinationLocalName is not null)
        {
            usedNames.Add(destinationLocalName);
        }

        for (var index = 0; index < result.Length; index++)
        {
            var mapping = result[index];

            if (mapping.ExplicitValueExpression is null ||
                !mapping.RequiresPreviousDestinationValueLocal)
            {
                continue;
            }

            if (mapping.ExplicitValueTypeName is null)
            {
                throw new InvalidOperationException(
                    "Explicit member mapping requires a value type.");
            }

            result[index] = mapping with
            {
                ValueLocalName =
                    MakeUniquePreviousDestinationValueLocalName(
                        mapping.DestinationMemberName,
                        usedNames)
            };
        }

        return result.ToImmutableArray();
    }

    private static string
        MakeUniquePreviousDestinationValueLocalName(
            string memberName,
            HashSet<string> usedNames)
    {
        var candidate =
            char.ToLowerInvariant(memberName[0]) +
            memberName.Substring(1);

        if (usedNames.Add(candidate))
        {
            return EscapeIdentifier(candidate);
        }

        for (var suffix = 1;; suffix++)
        {
            var name =
                candidate +
                suffix.ToString(CultureInfo.InvariantCulture);

            if (usedNames.Add(name))
            {
                return EscapeIdentifier(name);
            }
        }
    }

    private static string EscapeIdentifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) !=
               SyntaxKind.None
            ? "@" + value
            : value;
    }

    private static TypeMapperFactoryMappingModel?
        BuildFactoryMapping(
            DestinationPlan destinationPlan,
            TemplateMappingPlan? template,
            ImmutableArray<TypeMapperMemberMappingModel>
                mapNewMemberMappings,
            INamedTypeSymbol mapperType)
    {
        if (template is not
            {
                ConstructionKind:
                    TemplateConstructionKind.ByFactory,
                FactoryExpression: { } factoryExpression
            })
        {
            return null;
        }

        return new TypeMapperFactoryMappingModel(
            factoryExpression,
            AllocateFactoryDestinationLocalName(mapperType),
            destinationPlan.MapExistingKind ==
                TypeMapperMapExistingKind.NullableValue &&
            !mapNewMemberMappings.IsEmpty
                ? AllocateDestinationValueLocalName(mapperType)
                : null);
    }

    private static ConventionConstructorMappingPlan?
        BuildConstructorMapping(
            ITypeSymbol source,
            ITypeSymbol? destination,
            ConventionMemberMappingPlan memberMappings,
            TemplateMappingPlan? template,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            CancellationToken cancellationToken)
    {
        if (template is not { } templateValue ||
            templateValue.ConstructionKind ==
                TemplateConstructionKind.None)
        {
            return ConventionConstructorMappingPlanner.Build(
                source,
                destination,
                memberMappings,
                compilation,
                mapperType,
                cancellationToken);
        }

        if (templateValue.ConstructionKind ==
            TemplateConstructionKind.TypeParameterParameterless)
        {
            return BuildTypeParameterConstructorMapping(
                destination,
                memberMappings);
        }

        if (templateValue.ConstructionKind ==
            TemplateConstructionKind.ByConvention)
        {
            return templateValue.ConventionConstructorMappings
                    .IsDefault
                ? null
                : ConventionConstructorMappingPlanner.Build(
                    source,
                    destination,
                    memberMappings,
                    templateValue.ConventionConstructorMappings,
                    compilation,
                    mapperType,
                    cancellationToken);
        }

        if (templateValue.ConstructionKind ==
            TemplateConstructionKind.ByFactory)
        {
            return null;
        }

        return templateValue.Constructor is { } constructor
            ? BuildTemplateConstructorMapping(
                destination,
                memberMappings,
                constructor,
                mapperType)
            : null;
    }

    private static ConventionMemberMappingPlan MergeMemberMappings(
        ConventionMemberMappingPlan convention,
        TemplateMappingPlan? template,
        ITypeSymbol? destination,
        CancellationToken cancellationToken)
    {
        if (template is not { } value ||
            value.MapNewDirectExpression is not null)
        {
            return convention;
        }

        var templateMemberNames =
            new HashSet<string>(StringComparer.Ordinal);
        var conventionMapNewByName =
            convention.MapNew.ToDictionary(
                static mapping =>
                    mapping.DestinationMemberName,
                StringComparer.Ordinal);
        var conventionMapExistingByName =
            convention.MapExisting.ToDictionary(
                static mapping =>
                    mapping.DestinationMemberName,
                StringComparer.Ordinal);
        var mapNew =
            ImmutableArray.CreateBuilder<
                TypeMapperMemberMappingModel>();
        var mapExisting =
            ImmutableArray.CreateBuilder<
                TypeMapperMemberMappingModel>();

        foreach (var templateMember in value.MemberMappings)
        {
            templateMemberNames.Add(templateMember.MemberName);

            if (templateMember.MapNewMapping is { } explicitMapNew)
            {
                mapNew.Add(explicitMapNew);
            }
            else if (templateMember.Kind ==
                         TemplateMemberMappingKind.Auto &&
                     conventionMapNewByName.TryGetValue(
                         templateMember.MemberName,
                         out var automaticMapNew))
            {
                mapNew.Add(automaticMapNew);
            }

            if (templateMember.MapExistingMapping is
                { } explicitMapExisting)
            {
                mapExisting.Add(explicitMapExisting);
            }
            else if (templateMember.Kind ==
                         TemplateMemberMappingKind.Auto &&
                     conventionMapExistingByName.TryGetValue(
                         templateMember.MemberName,
                         out var automaticMapExisting))
            {
                mapExisting.Add(automaticMapExisting);
            }
        }

        mapNew.AddRange(
            convention.MapNew.Where(mapping =>
                !templateMemberNames.Contains(
                    mapping.DestinationMemberName)));
        mapExisting.AddRange(
            convention.MapExisting.Where(mapping =>
                !templateMemberNames.Contains(
                    mapping.DestinationMemberName)));

        var mapNewMappings = mapNew.ToImmutable();

        return new ConventionMemberMappingPlan(
            mapNewMappings,
            mapExisting.ToImmutable(),
            TemplateMappingPlanner.HasUnmappedRequiredMembers(
                destination,
                mapNewMappings,
                cancellationToken));
    }

    private static ConventionConstructorMappingPlan?
        BuildTypeParameterConstructorMapping(
            ITypeSymbol? destination,
            ConventionMemberMappingPlan memberMappings)
    {
        if (destination is not ITypeParameterSymbol typeParameter ||
            memberMappings.HasUnmappedRequiredMembers ||
            !typeParameter.HasValueTypeConstraint &&
            !typeParameter.HasUnmanagedTypeConstraint &&
            !typeParameter.HasConstructorConstraint)
        {
            return null;
        }

        return new ConventionConstructorMappingPlan(
            new TypeMapperConstructorMappingModel(
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    destination),
                []),
            memberMappings.MapNew);
    }

    private static ConventionConstructorMappingPlan?
        BuildTemplateConstructorMapping(
            ITypeSymbol? destination,
            ConventionMemberMappingPlan memberMappings,
            TemplateConstructorMappingPlan templateConstructor,
            INamedTypeSymbol mapperType)
    {
        var setsRequiredMembers =
            ConventionConstructorMappingPlanner
                .HasSetsRequiredMembersAttribute(
                    templateConstructor.Constructor);

        if (destination is null ||
            memberMappings.HasUnmappedRequiredMembers &&
            !setsRequiredMembers)
        {
            return null;
        }

        var correspondingMemberIndexes =
            new HashSet<int>();

        foreach (var parameter in
                 templateConstructor.Constructor.Parameters)
        {
            if (templateConstructor.IgnoredParameterNames.Contains(
                    parameter.Name,
                    StringComparer.Ordinal))
            {
                continue;
            }

            if (FindCorrespondingMemberIndex(
                    memberMappings.MapNew,
                    parameter.Name) is { } memberIndex)
            {
                correspondingMemberIndexes.Add(memberIndex);
            }
        }

        var correspondingArgumentIndexes =
            new List<int>[memberMappings.MapNew.Length];

        for (var argumentIndex = 0;
             argumentIndex < templateConstructor.Arguments.Length;
             argumentIndex++)
        {
            if (FindCorrespondingMemberIndex(
                    memberMappings.MapNew,
                    templateConstructor.Arguments[argumentIndex]
                        .ParameterName) is not { } memberIndex)
            {
                continue;
            }

            correspondingArgumentIndexes[memberIndex] ??=
                new List<int>();
            correspondingArgumentIndexes[memberIndex]!
                .Add(argumentIndex);
        }

        var mapNew =
            ImmutableArray.CreateBuilder<
                TypeMapperMemberMappingModel>();
        var sharedValues =
            new List<(int MemberIndex, int ArgumentIndex)>();

        for (var index = 0;
             index < memberMappings.MapNew.Length;
             index++)
        {
            var mapping = memberMappings.MapNew[index];

            if (!correspondingMemberIndexes.Contains(index) ||
                mapping.ExplicitValueExpression is not null ||
                mapping.IsRequired && !setsRequiredMembers)
            {
                if (correspondingMemberIndexes.Contains(index) &&
                    mapping.ExplicitValueExpression is null &&
                    mapping.IsRequired &&
                    !setsRequiredMembers &&
                    correspondingArgumentIndexes[index] is
                        { Count: 1 } argumentIndexes)
                {
                    var argumentIndex = argumentIndexes[0];
                    var argument =
                        templateConstructor.Arguments[
                            argumentIndex];

                    if (argument.ExplicitValueExpression is null &&
                        StringComparer.Ordinal.Equals(
                            argument.SourceMemberName,
                            mapping.SourceMemberName))
                    {
                        sharedValues.Add(
                            (mapNew.Count, argumentIndex));
                    }
                }

                mapNew.Add(mapping);
            }
        }

        var argumentModels =
            templateConstructor.Arguments.ToArray();

        if (sharedValues.Count > 0)
        {
            var lastSharedArgumentIndex =
                sharedValues.Max(
                    static value =>
                        value.ArgumentIndex);
            var usedValueLocalNames =
                ConventionConstructorMappingPlanner
                    .BuildUsedValueLocalNames(mapperType);

            for (var argumentIndex = 0;
                 argumentIndex <= lastSharedArgumentIndex;
                 argumentIndex++)
            {
                var argument = argumentModels[argumentIndex];

                argumentModels[argumentIndex] =
                    argument with
                    {
                        ValueLocalName =
                            argument.ExplicitValueExpression is not null
                                ? ConventionConstructorMappingPlanner
                                    .MakeUniqueValueLocalName(
                                        "template",
                                        argument.ParameterName,
                                        usedValueLocalNames)
                                : ConventionConstructorMappingPlanner
                                    .MakeUniqueSourceValueLocalName(
                                        argument.SourceMemberName,
                                        usedValueLocalNames)
                    };
            }

            foreach (var sharedValue in sharedValues)
            {
                var memberMapping =
                    mapNew[sharedValue.MemberIndex];

                mapNew[sharedValue.MemberIndex] =
                    memberMapping with
                    {
                        SourceValueLocalName =
                            argumentModels[
                                sharedValue.ArgumentIndex]
                                .ValueLocalName
                    };
            }
        }

        return new ConventionConstructorMappingPlan(
            new TypeMapperConstructorMappingModel(
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    destination),
                argumentModels.ToImmutableArray()),
            mapNew.ToImmutable());
    }

    private static int? FindCorrespondingMemberIndex(
        ImmutableArray<TypeMapperMemberMappingModel> memberMappings,
        string parameterName)
    {
        for (var index = 0;
             index < memberMappings.Length;
             index++)
        {
            if (StringComparer.Ordinal.Equals(
                    memberMappings[index].DestinationMemberName,
                    parameterName))
            {
                return index;
            }
        }

        int? result = null;

        for (var index = 0;
             index < memberMappings.Length;
             index++)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(
                    memberMappings[index].DestinationMemberName,
                    parameterName))
            {
                continue;
            }

            if (result is not null)
            {
                return null;
            }

            result = index;
        }

        return result;
    }

    private static DestinationPlan BuildDestinationPlan(
        ITypeSymbol destinationType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var memberType = destinationType;
        var isNullableValue = false;

        if (destinationType is INamedTypeSymbol namedDestination)
        {
            if (DirectDestinationTypePolicy.IsDirect(
                    namedDestination))
            {
                return default;
            }

            if (namedDestination.OriginalDefinition.SpecialType ==
                    SpecialType.System_Nullable_T)
            {
                memberType = namedDestination.TypeArguments[0];
                isNullableValue = true;
            }
        }

        memberType = memberType.WithNullableAnnotation(
            NullableAnnotation.NotAnnotated);

        if (memberType is ITypeParameterSymbol typeParameter)
        {
            return new DestinationPlan(
                memberType,
                isNullableValue
                    ? TypeMapperMapExistingKind.NullableValue
                    : GetTypeParameterMapExistingKind(
                        typeParameter,
                        cancellationToken));
        }

        if (memberType is not INamedTypeSymbol namedMemberType ||
            namedMemberType.IsRefLikeType)
        {
            return default;
        }

        return namedMemberType.TypeKind switch
        {
            TypeKind.Class or TypeKind.Interface =>
                new DestinationPlan(
                    memberType,
                    TypeMapperMapExistingKind.Reference),
            TypeKind.Struct =>
                new DestinationPlan(
                    memberType,
                    isNullableValue
                        ? TypeMapperMapExistingKind.NullableValue
                        : TypeMapperMapExistingKind.Value),
            _ => default
        };
    }

    private static TypeMapperMapExistingKind
        GetTypeParameterMapExistingKind(
            ITypeParameterSymbol typeParameter,
            CancellationToken cancellationToken)
    {
        if (!HasMapExistingConstraint(
                typeParameter,
                new HashSet<ISymbol>(
                    SymbolEqualityComparer.Default),
                cancellationToken))
        {
            return TypeMapperMapExistingKind.Unsupported;
        }

        return typeParameter.HasValueTypeConstraint
            ? TypeMapperMapExistingKind.Value
            : TypeMapperMapExistingKind.Reference;
    }

    private static bool HasMapExistingConstraint(
        ITypeParameterSymbol typeParameter,
        HashSet<ISymbol> visitedTypeParameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!visitedTypeParameters.Add(typeParameter))
        {
            return false;
        }

        if (typeParameter.HasReferenceTypeConstraint)
        {
            return true;
        }

        foreach (var constraint in typeParameter.ConstraintTypes)
        {
            if (constraint is ITypeParameterSymbol
                    nestedTypeParameter)
            {
                if (HasMapExistingConstraint(
                        nestedTypeParameter,
                        visitedTypeParameters,
                        cancellationToken))
                {
                    return true;
                }

                continue;
            }

            if (constraint.TypeKind is
                TypeKind.Class or
                TypeKind.Interface)
            {
                return true;
            }
        }

        return false;
    }

    private static string AllocateDestinationValueLocalName(
        INamedTypeSymbol mapperType)
    {
        var usedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "source",
            "destination",
            "context"
        };

        for (var type = mapperType;
             type is not null;
             type = type.ContainingType)
        {
            foreach (var typeParameter in type.TypeParameters)
            {
                usedNames.Add(typeParameter.Name);
            }
        }

        const string candidate = "destinationValue";

        if (usedNames.Add(candidate))
        {
            return candidate;
        }

        for (var suffix = 1;; suffix++)
        {
            var name =
                candidate +
                suffix.ToString(CultureInfo.InvariantCulture);

            if (usedNames.Add(name))
            {
                return name;
            }
        }
    }

    private static string AllocateFactoryDestinationLocalName(
        INamedTypeSymbol mapperType)
    {
        var usedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "source",
            "context"
        };

        for (var type = mapperType;
             type is not null;
             type = type.ContainingType)
        {
            foreach (var typeParameter in type.TypeParameters)
            {
                usedNames.Add(typeParameter.Name);
            }
        }

        const string candidate = "destination";

        if (usedNames.Add(candidate))
        {
            return candidate;
        }

        for (var suffix = 1;; suffix++)
        {
            var name =
                candidate +
                suffix.ToString(CultureInfo.InvariantCulture);

            if (usedNames.Add(name))
            {
                return name;
            }
        }
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

    private readonly record struct DestinationPlan(
        ITypeSymbol? MemberType,
        TypeMapperMapExistingKind MapExistingKind);
}
