using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TypeMapperPipeline
{
    private const string ConfiguredPlanUnsupportedMessage =
        "Configured Construct, Members, and Convert plans are not executable yet.";

    private const string ConventionConstructionUnavailableMessage =
        "Convention construction is not available for this destination.";

    private const string ConstructorSelectionUnsupportedMessage =
        "The effective ConstructorSelection is not supported yet.";

    private const string MemberSelectionUnsupportedMessage =
        "The effective MemberSelection is not supported yet.";

    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValueProvider<MappingSettings> assemblySettings,
        IncrementalValuesProvider<MapperPairConfigurationModel>
            mapperConfigurations)
    {
        var requests = mapperConfigurations
            .Combine(compilationContext)
            .Combine(assemblySettings)
            .Select(static (source, cancellationToken) =>
                TryBuildGenerationInput(source, cancellationToken))
            .WhereHasValue()
            .Collect()
            .SelectMany(static (generationInputs, cancellationToken) =>
                BuildRequests(generationInputs, cancellationToken))
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
            (
                MapperPairConfigurationModel Configuration,
                CompilationContext Context
            ) Input,
            MappingSettings AssemblySettings
        ) source,
        CancellationToken cancellationToken)
    {
        var ((configuration, context), assemblySettings) = source;
        var configureSyntax = configuration.MappingPairs.ConfigureSyntax;
        var semanticModel = context.Compilation.GetSemanticModel(
            configureSyntax.SyntaxTree);

        if (configureSyntax.Parent is not
                ClassDeclarationSyntax mapperDeclaration ||
            semanticModel.GetDeclaredSymbol(
                mapperDeclaration,
                cancellationToken) is not INamedTypeSymbol mapperType ||
            !CanGenerate(mapperType, mapperDeclaration) ||
            context.Compilation is not CSharpCompilation compilation)
        {
            return null;
        }

        var mappings = BuildMappings(
            configuration,
            assemblySettings,
            compilation,
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
            BuildTypeParameterList(mapperDeclaration.TypeParameterList),
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
        var requests = ImmutableArray.CreateBuilder<TypeMapperRequest>(
            orderedInputs.Length);

        foreach (var generationInput in orderedInputs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            requests.Add(
                new TypeMapperRequest(
                    GeneratedSourceHintName.Create(
                        "TypeMapper",
                        hintNamePartAllocator.Allocate(
                            generationInput.StableIdentity)),
                    generationInput.Model));
        }

        return requests.ToImmutable();
    }

    private static ImmutableArray<TypeMapperMappingModel> BuildMappings(
        MapperPairConfigurationModel configuration,
        MappingSettings assemblySettings,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (configuration.MappingPairs.HasUnifiablePairs)
        {
            return default;
        }

        var usedGeneratedMethodNames = BuildUsedGeneratedMethodNames(
            mapperType);
        var mappings = ImmutableArray.CreateBuilder<TypeMapperMappingModel>(
            configuration.Pairs.Length);

        foreach (var pairConfiguration in configuration.Pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var effectiveSettings = EffectiveMappingSettings.Resolve(
                assemblySettings,
                ToMappingSettings(configuration.RootSettings),
                ToMappingSettings(pairConfiguration.Settings));
            var mapping = BuildMapping(
                pairConfiguration,
                configuration.RootSettings,
                compilation,
                mapperType,
                cancellationToken);
            var createMethodName = RequiresCreateMethod(
                    mapping,
                    effectiveSettings)
                ? AllocateName("Create", usedGeneratedMethodNames)
                : null;

            mappings.Add(
                mapping with
                {
                    EffectiveSettings = effectiveSettings,
                    MapNewImplMethodName = createMethodName,
                    MapNewImplUsesContext = false
                });
        }

        return mappings.ToImmutable();
    }

    private static TypeMapperMappingModel BuildMapping(
        PairConfigurationModel configuration,
        PairConfigurationSettings rootSettings,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var pair = configuration.Pair;
        var declarativeSourceType =
            MappingTypeNormalization.NormalizeDeclarativeSource(
                pair.SourceType,
                compilation);
        var nonNullSourceName = BuildNonNullSourceName(
            pair.SourceType,
            mapperType);
        var destinationPlan = BuildDestinationPlan(
            pair.DestinationType,
            compilation,
            cancellationToken);
        var mapping = BuildEmptyMapping(
            pair,
            destinationPlan,
            declarativeSourceType,
            nonNullSourceName);

        if (configuration.Conflicts != PairConfigurationConflict.None ||
            !configuration.Declarative.Constructs.IsEmpty ||
            !configuration.Declarative.Members.IsEmpty ||
            !configuration.Manual.Conversions.IsEmpty)
        {
            return mapping with
            {
                UnsupportedExceptionMessage =
                    ConfiguredPlanUnsupportedMessage
            };
        }

        var memberSelection = ResolveSetting(
            configuration.Settings.MemberSelection,
            rootSettings.MemberSelection,
            MemberSelectionValue.Auto);

        if (memberSelection != MemberSelectionValue.Auto)
        {
            return mapping with
            {
                UnsupportedExceptionMessage =
                    MemberSelectionUnsupportedMessage
            };
        }

        var memberMappings = ConventionMemberMappingPlanner.Build(
            declarativeSourceType,
            destinationPlan.MemberType,
            pair.Capabilities,
            compilation,
            mapperType,
            cancellationToken);
        var constructorSelection = ResolveSetting(
            configuration.Settings.ConstructorSelection,
            rootSettings.ConstructorSelection,
            ConstructorSelectionValue.Unambiguous);
        ConventionConstructorMappingPlan? constructorMapping = null;
        string? createUnsupportedMessage = null;

        if (constructorSelection != ConstructorSelectionValue.Unambiguous)
        {
            createUnsupportedMessage =
                ConstructorSelectionUnsupportedMessage;
        }
        else
        {
            constructorMapping = ConventionConstructorMappingPlanner.Build(
                declarativeSourceType,
                destinationPlan.MemberType,
                memberMappings,
                pair.Capabilities,
                compilation,
                mapperType,
                nonNullSourceName,
                cancellationToken);

            if (constructorMapping is null)
            {
                createUnsupportedMessage =
                    ConventionConstructionUnavailableMessage;
            }
        }

        var mapExistingDestinationLocalName =
            destinationPlan.MapExistingKind ==
                TypeMapperMapExistingKind.NullableValue &&
            !memberMappings.MapExisting.IsEmpty
                ? BuildDestinationValueLocalName(mapperType)
                : null;

        return mapping with
        {
            MapNewConstructor = constructorMapping?.Constructor,
            MapExistingDestinationLocalName =
                mapExistingDestinationLocalName,
            MapNewMemberMappings =
                constructorMapping?.MapNewMemberMappings ??
                memberMappings.MapNew,
            MapExistingMemberMappings = memberMappings.MapExisting,
            MapNewUnsupportedExceptionMessage =
                createUnsupportedMessage
        };
    }

    private static TypeMapperMappingModel BuildEmptyMapping(
        MappingPairModel pair,
        DestinationPlan destinationPlan,
        ITypeSymbol declarativeSourceType,
        string nonNullSourceName)
    {
        return new TypeMapperMappingModel(
            SourceTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    pair.SourceType),
            MaybeNullSourceTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedMaybeNullTypeName(
                    pair.SourceType),
            NonNullSourceTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    declarativeSourceType),
            NonNullSourceName: nonNullSourceName,
            DestinationTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    pair.DestinationType),
            MaybeNullDestinationTypeName:
                TypeMapperMappingTypePolicy
                    .GetGeneratedMaybeNullTypeName(
                        pair.DestinationType),
            SourceCanBeNull: CanBeNull(pair.SourceType),
            SourceIsNullableValue:
                MappingTypeNormalization.IsNullableValue(
                    pair.SourceType),
            DestinationCanBeNull: CanBeNull(pair.DestinationType),
            MapNewDirectExpression: null,
            MapExistingDirectExpression: null,
            MapNewFactory: null,
            MapNewConstructor: null,
            MapExistingKind: destinationPlan.MapExistingKind,
            MapExistingDestinationLocalName: null,
            MapNewMemberMappings: [],
            MapExistingMemberMappings: []);
    }

    private static DestinationPlan BuildDestinationPlan(
        ITypeSymbol destinationType,
        CSharpCompilation compilation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isNullableValue = destinationType is INamedTypeSymbol named &&
            named.OriginalDefinition.SpecialType ==
                SpecialType.System_Nullable_T;
        var memberType = DestinationCapabilityPolicy.GetDestinationType(
                destinationType,
                compilation)
            .WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        var mapExistingKind = memberType.TypeKind switch
        {
            TypeKind.Class or TypeKind.Interface =>
                TypeMapperMapExistingKind.Reference,
            TypeKind.Struct or TypeKind.Enum =>
                isNullableValue
                    ? TypeMapperMapExistingKind.NullableValue
                    : TypeMapperMapExistingKind.Value,
            _ => TypeMapperMapExistingKind.Unsupported
        };

        return new DestinationPlan(memberType, mapExistingKind);
    }

    private static string BuildNonNullSourceName(
        ITypeSymbol sourceType,
        INamedTypeSymbol mapperType)
    {
        if (!MappingTypeNormalization.IsNullableValue(sourceType))
        {
            return "source";
        }

        var usedNames =
            ConventionConstructorMappingPlanner.BuildUsedValueLocalNames(
                mapperType);
        usedNames.Add("destination");

        return AllocateName("sourceValue", usedNames);
    }

    private static MappingSettings ToMappingSettings(
        PairConfigurationSettings settings)
    {
        return new MappingSettings(
            GetSettingOrDefault(
                settings.MappingMode,
                MappingModeValue.Default),
            GetSettingOrDefault(
                settings.NullSourceHandling,
                NullSourceHandlingValue.Default),
            GetSettingOrDefault(
                settings.NullDestinationHandling,
                NullDestinationHandlingValue.Default));
    }

    private static TValue? GetSettingOrDefault<TValue>(
        PairConfigurationSetting<TValue> setting,
        TValue defaultValue)
        where TValue : struct, Enum
    {
        return setting.Origin == PairConfigurationSettingOrigin.Unset
            ? defaultValue
            : setting.Value;
    }

    private static TValue? ResolveSetting<TValue>(
        PairConfigurationSetting<TValue> pairSetting,
        PairConfigurationSetting<TValue> rootSetting,
        TValue defaultValue)
        where TValue : struct, Enum
    {
        foreach (var setting in new[]
                 {
                     pairSetting,
                     rootSetting
                 })
        {
            if (setting.Origin == PairConfigurationSettingOrigin.Unset)
            {
                continue;
            }

            if (setting.Value is not { } value)
            {
                return null;
            }

            if (!EqualityComparer<TValue>.Default.Equals(value, default))
            {
                return value;
            }
        }

        return defaultValue;
    }

    private static bool RequiresCreateMethod(
        TypeMapperMappingModel mapping,
        EffectiveMappingSettings settings)
    {
        return mapping.DestinationCanBeNull &&
               settings.IsNullSourceHandlingValid &&
               settings.SupportsMapNew &&
               settings.SupportsMapExisting &&
               settings.NullDestinationHandling ==
                   NullDestinationHandlingValue.Create;
    }

    private static bool CanBeNull(ITypeSymbol type)
    {
        if (type.IsReferenceType)
        {
            return true;
        }

        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType ==
                SpecialType.System_Nullable_T)
        {
            return true;
        }

        return type is ITypeParameterSymbol typeParameter &&
               !typeParameter.HasValueTypeConstraint &&
               !typeParameter.HasUnmanagedTypeConstraint;
    }

    private static string BuildDestinationValueLocalName(
        INamedTypeSymbol mapperType)
    {
        var usedNames =
            ConventionConstructorMappingPlanner.BuildUsedValueLocalNames(
                mapperType);
        usedNames.Add("destination");

        return AllocateName("destinationValue", usedNames);
    }

    private static HashSet<string> BuildUsedGeneratedMethodNames(
        INamedTypeSymbol mapperType)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        for (var type = mapperType;
             type is not null;
             type = type.ContainingType)
        {
            result.Add(type.Name);

            foreach (var typeParameter in type.TypeParameters)
            {
                result.Add(typeParameter.Name);
            }
        }

        for (var type = mapperType;
             type is not null;
             type = type.BaseType)
        {
            foreach (var member in type.GetMembers())
            {
                result.Add(member.Name);
            }
        }

        return result;
    }

    private static string AllocateName(
        string preferredName,
        HashSet<string> usedNames)
    {
        if (usedNames.Add(preferredName))
        {
            return preferredName;
        }

        for (var suffix = 1;; suffix++)
        {
            var candidate = preferredName + suffix;

            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static bool CanGenerate(
        INamedTypeSymbol mapperType,
        ClassDeclarationSyntax mapperDeclaration)
    {
        if (!IsPartial(mapperDeclaration) ||
            !IsSupportedAccessibility(mapperType.DeclaredAccessibility) ||
            mapperDeclaration
                .Ancestors()
                .OfType<TypeDeclarationSyntax>()
                .Any(static declaration => !IsPartial(declaration)))
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

    private static bool IsPartial(TypeDeclarationSyntax declaration)
    {
        return declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
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

    private static ImmutableArray<TypeMapperContainingTypeModel>
        BuildContainingTypes(ClassDeclarationSyntax mapperDeclaration)
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
        return typeParameterList is null
            ? string.Empty
            : "<" +
              string.Join(
                  ", ",
                  typeParameterList.Parameters.Select(
                      static parameter => parameter.Identifier.Text)) +
              ">";
    }

    private readonly record struct TypeMapperGenerationInput(
        string StableIdentity,
        TypeMapperModel Model);

    private readonly record struct DestinationPlan(
        ITypeSymbol MemberType,
        TypeMapperMapExistingKind MapExistingKind);
}
