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
        "Configured plan conflicts and Convert plans are not executable yet.";

    private const string ConventionConstructionUnavailableMessage =
        "Convention construction is not available for this destination.";

    private const string ConstructorSelectionUnsupportedMessage =
        "The effective ConstructorSelection is not supported yet.";

    private const string InvalidMemberSelectionMessage =
        "The effective MemberSelection is invalid.";

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
                effectiveSettings,
                compilation,
                mapperType,
                usedGeneratedMethodNames,
                cancellationToken);
            var createMethodName = RequiresCreateMethod(
                    mapping,
                    effectiveSettings)
                ? AllocateName("CreateImpl", usedGeneratedMethodNames)
                : null;
            var updateMethodName = RequiresUpdateMethod(effectiveSettings)
                ? AllocateName("UpdateImpl", usedGeneratedMethodNames)
                : null;

            mappings.Add(
                mapping with
                {
                    EffectiveSettings = effectiveSettings,
                    MapNewImplMethodName = createMethodName,
                    MapExistingImplMethodName = updateMethodName
                });
        }

        return mappings.ToImmutable();
    }

    private static TypeMapperMappingModel BuildMapping(
        PairConfigurationModel configuration,
        PairConfigurationSettings rootSettings,
        EffectiveMappingSettings effectiveSettings,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        HashSet<string> usedGeneratedMethodNames,
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
            nonNullSourceName,
            mapperType);

        if (configuration.Conflicts != PairConfigurationConflict.None ||
            !configuration.Manual.Conversions.IsEmpty)
        {
            return mapping with
            {
                UnsupportedExceptionMessage =
                    ConfiguredPlanUnsupportedMessage
            };
        }

        if (!effectiveSettings.IsMemberSelectionValid)
        {
            return mapping with
            {
                UnsupportedExceptionMessage =
                    InvalidMemberSelectionMessage
            };
        }

        var conventionMemberMappings =
            ConventionMemberMappingPlanner.Build(
            declarativeSourceType,
            destinationPlan.MemberType,
            pair.Capabilities,
            compilation,
            mapperType,
            cancellationToken);
        var members = BasicMembersMappingPlanner.Build(
            configuration.Declarative.Members.IsEmpty
                ? null
                : configuration.Declarative.Members[0],
            effectiveSettings.MemberSelection!.Value,
            mapping,
            conventionMemberMappings,
            destinationPlan.MemberType,
            pair.Capabilities,
            compilation,
            mapperType,
            cancellationToken);

        if (members.UnsupportedMessage is { } membersUnsupportedMessage)
        {
            return mapping with
            {
                UnsupportedExceptionMessage = membersUnsupportedMessage
            };
        }

        var memberMappings = members.Plan;
        var constructorSelection = ResolveSetting(
            configuration.Settings.ConstructorSelection,
            rootSettings.ConstructorSelection,
            ConstructorSelectionValue.Unambiguous);

        if (!configuration.Declarative.Constructs.IsEmpty)
        {
            if (pair.Capabilities.DirectConstruction)
            {
                var directConstruct =
                    DirectConstructMappingPlanner.Build(
                        configuration.Declarative.Constructs[0],
                        mapping,
                        memberMappings,
                        mapperType,
                        usedGeneratedMethodNames,
                        cancellationToken);

                return mapping with
                {
                    ControlFlow = directConstruct.ControlFlow,
                    HelperMethodDeclarations =
                        directConstruct.HelperMethodDeclarations,
                    UnsupportedExceptionMessage =
                        directConstruct.UnsupportedMessage
                };
            }

            if (destinationPlan.MemberType is not
                INamedTypeSymbol structuredDestination)
            {
                return mapping with
                {
                    UnsupportedExceptionMessage =
                        "Configured Construct plans are not executable yet."
                };
            }

            var structuredConstruct =
                StructuredConstructMappingPlanner.Build(
                    configuration.Declarative.Constructs[0],
                    mapping,
                    declarativeSourceType,
                    structuredDestination,
                    pair.Capabilities,
                    memberMappings,
                constructorSelection,
                compilation,
                mapperType,
                usedGeneratedMethodNames,
                cancellationToken);

            return mapping with
            {
                ControlFlow = structuredConstruct.ControlFlow,
                HelperMethodDeclarations =
                    structuredConstruct.HelperMethodDeclarations,
                UnsupportedExceptionMessage =
                    structuredConstruct.UnsupportedMessage
            };
        }

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
                memberMappings.BuildConstructorPlan(
                    replacement: false),
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

        return mapping with
        {
            MapNewConstructor = constructorMapping?.Constructor,
            MapNewMemberMappings =
                constructorMapping?.MapNewMemberMappings ??
                memberMappings.MapNew,
            MapNewPostMemberMappings =
                constructorMapping?.MapNewPostMemberMappings ??
                [],
            MapExistingMemberMappings = memberMappings.MapExisting,
            MapNewUnsupportedExceptionMessage =
                createUnsupportedMessage
        };
    }

    private static TypeMapperMappingModel BuildEmptyMapping(
        MappingPairModel pair,
        DestinationPlan destinationPlan,
        ITypeSymbol declarativeSourceType,
        string nonNullSourceName,
        INamedTypeSymbol mapperType)
    {
        var usedLocalNames =
            UserResultMappingPlanner.BuildUsedLocalNames(mapperType);
        usedLocalNames.Add(nonNullSourceName);
        var resultLocalName =
            UserResultMappingPlanner.AllocateName(
                "result",
                usedLocalNames);

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
            NonNullDestinationTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    destinationPlan.MemberType),
            ResultLocalName: resultLocalName,
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
            MapNewMemberMappings: [],
            MapNewPostMemberMappings: [],
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
                NullDestinationHandlingValue.Default),
            GetSettingOrDefault(
                settings.MemberSelection,
                MemberSelectionValue.Default));
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
        if (!settings.IsMappingModeValid ||
            !settings.IsNullSourceHandlingValid)
        {
            return false;
        }

        return settings.SupportsMapNew ||
               mapping.DestinationCanBeNull &&
               settings.SupportsMapExisting &&
               settings.IsNullDestinationHandlingValid &&
               settings.NullDestinationHandling ==
                   NullDestinationHandlingValue.Create;
    }

    private static bool RequiresUpdateMethod(
        EffectiveMappingSettings settings)
    {
        return settings.IsMappingModeValid &&
               settings.SupportsMapExisting &&
               settings.IsNullSourceHandlingValid &&
               settings.IsNullDestinationHandlingValid;
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
