using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.Incrementality;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TypeMapperPipeline
{
    private const string InvalidBaseConfigurationMessage =
        "The mapper inheritance chain contains a base mapper whose " +
        "configuration cannot be composed.";

    private const string ConventionConstructionUnavailableMessage =
        "Convention construction is not available for this destination.";

    private const string InvalidConstructorSelectionMessage =
        "The effective ConstructorSelection is invalid.";

    private const string DirectConstructorSelectionConflictMessage =
        "The configured map-level ConstructorSelection is not applicable " +
        "to direct construction.";

    private const string InvalidMemberSelectionMessage =
        "The effective MemberSelection is invalid.";

    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValueProvider<MappingSettings> assemblySettings,
        IncrementalValuesProvider<MapperPairConfigurationModel>
            mapperConfigurations)
    {
        var models = mapperConfigurations
            .Combine(compilationContext)
            .Combine(assemblySettings)
            .Select(static (source, cancellationToken) =>
                TryBuildGenerationInput(source, cancellationToken))
            .WhereHasValue()
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildTypeMapperModels);
        var hintNameAllocations = models
            .Select(static (model, _) =>
                new HintNameIdentity(
                    model.StableIdentity,
                    HintNameHelper.ToHintNamePart(
                        model.StableIdentity)))
            .Collect()
            .Select(static (identities, cancellationToken) =>
                HintNameCollisions.Build(
                    identities,
                    cancellationToken))
            .WithComparer(HintNameAllocationsComparer.Instance);
        var requests = models
            .Combine(hintNameAllocations)
            .Select(static (source, _) =>
                BuildRequest(source.Left, source.Right))
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildTypeMapperRequests);

        context.RegisterSourceOutput(
            requests,
            static (context, request) =>
                context.AddSource(
                    request.HintName,
                    request.Source));
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
            mappings,
            configureSyntax.DescendantNodes()
                .OfType<QueryExpressionSyntax>()
                .Any());

        return new TypeMapperGenerationInput(
            SymbolNameHelper.GetFullMetadataName(mapperType),
            TypeMapperEmitter.Emit(model).ToString());
    }

    private static TypeMapperRequest BuildRequest(
        TypeMapperGenerationInput input,
        HintNameAllocations allocations)
    {
        return new TypeMapperRequest(
            GeneratedSourceHintName.Create(
                "TypeMapper",
                HintNameCollisions.Resolve(
                    allocations,
                    input.StableIdentity)),
            input.Source);
    }

    private static ImmutableArray<TypeMapperMappingModel> BuildMappings(
        MapperPairConfigurationModel configuration,
        MappingSettings assemblySettings,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var usedGeneratedMethodNames = BuildUsedGeneratedMethodNames(
            mapperType);
        var mappings = ImmutableArray.CreateBuilder<OrderedMapping>(
            configuration.Pairs.Length +
            configuration.MappingPairs.UnsupportedPairs.Length);

        foreach (var pairConfiguration in configuration.Pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pairConfiguration.Pair.HasUnifiableConflict)
            {
                continue;
            }

            var effectiveSettings = EffectiveMappingSettings.Resolve(
                assemblySettings,
                new[]
                {
                    ToMappingSettings(pairConfiguration.Settings)
                }.Concat(
                    pairConfiguration.Composition.IncludedBaseSettings
                        .Select(ToMappingSettings)),
                new[]
                {
                    ToMappingSettings(configuration.RootSettings)
                }.Concat(
                    configuration.BaseRootSettings
                        .Select(ToMappingSettings)));
            var mapping = BuildMapping(
                pairConfiguration,
                effectiveSettings,
                compilation,
                mapperType,
                usedGeneratedMethodNames,
                cancellationToken);

            if (configuration.HasInvalidBaseConfiguration)
            {
                mapping = mapping with
                {
                    UnsupportedExceptionMessage =
                        InvalidBaseConfigurationMessage
                };
            }
            var createMethodName = RequiresCreateMethod(
                    mapping,
                    effectiveSettings)
                ? AllocateName("__Create", usedGeneratedMethodNames)
                : null;
            var updateMethodName = RequiresUpdateMethod(
                    mapping,
                    effectiveSettings)
                ? AllocateName("__Update", usedGeneratedMethodNames)
                : null;
            var createImplUsesOperation =
                createMethodName is not null &&
                CreatePathNeedsOperationParameter(mapping);

            mappings.Add(new OrderedMapping(
                pairConfiguration.Pair.Registration.Syntax.SpanStart,
                mapping with
                {
                    EffectiveSettings = effectiveSettings,
                    CreateImplMethodName = createMethodName,
                    UpdateImplMethodName = updateMethodName,
                    CreateImplUsesOperation = createImplUsesOperation
                }));
        }

        foreach (var unsupportedPair in
                 configuration.MappingPairs.UnsupportedPairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (unsupportedPair.HasUnifiableConflict)
            {
                continue;
            }

            mappings.Add(new OrderedMapping(
                unsupportedPair.Registration.Syntax.SpanStart,
                BuildUnsupportedMapping(
                    unsupportedPair,
                    compilation,
                    mapperType)));
        }

        return mappings
            .OrderBy(static mapping => mapping.Position)
            .Select(static mapping => mapping.Mapping)
            .ToImmutableArray();
    }

    private static TypeMapperMappingModel BuildUnsupportedMapping(
        UnsupportedMappingPairModel pair,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType)
    {
        var declarativeSourceType =
            MappingTypeNormalization.NormalizeDeclarativeSource(
                pair.SourceType,
                compilation);
        var previousDestinationType =
            MappingTypeNormalization.NormalizePreviousDestination(
                pair.DestinationType,
                compilation);
        var nonNullSourceName = BuildNonNullSourceName(
            pair.SourceType,
            mapperType);
        var usedLocalNames =
            UserResultMappingPlanner.BuildUsedLocalNames(mapperType);
        usedLocalNames.Add(nonNullSourceName);

        return new TypeMapperMappingModel(
            SourceTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    pair.SourceType),
            SourceRuntimeTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedRuntimeTypeName(
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
            DestinationRuntimeTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedRuntimeTypeName(
                    pair.DestinationType),
            MaybeNullDestinationTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedMaybeNullTypeName(
                    pair.DestinationType),
            NonNullDestinationTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    previousDestinationType),
            ResultLocalName: AllocateName("result", usedLocalNames),
            SourceCanBeNull: CanBeNull(pair.SourceType),
            SourceIsNullableValue:
                MappingTypeNormalization.IsNullableValue(pair.SourceType),
            DestinationCanBeNull: CanBeNull(pair.DestinationType),
            CreateDirectExpression: null,
            UpdateDirectExpression: null,
            CreateFactory: null,
            CreateConstructor: null,
            UpdateKind: TypeMapperUpdateKind.Unsupported,
            CreateMemberMappings: [],
            CreatePostMemberMappings: [],
            UpdateMemberMappings: [],
            UnsupportedExceptionMessage: pair.Reason);
    }

    private static TypeMapperMappingModel BuildMapping(
        PairConfigurationModel configuration,
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

        if (configuration.Conflicts != PairConfigurationConflict.None)
        {
            return mapping with
            {
                ManualMapping = configuration.Manual.Conversions.IsEmpty
                    ? null
                    : new TypeMapperManualMappingModel(
                        null,
                        configuration.Manual.Conversions[0].Form),
                UnsupportedExceptionMessage =
                    BuildConfiguredPlanConflictMessage(
                        configuration.Conflicts)
            };
        }

        if (!configuration.Manual.Conversions.IsEmpty)
        {
            if (HasExplicitManualSettingConflict(configuration.Settings))
            {
                return mapping with
                {
                    ManualMapping = new TypeMapperManualMappingModel(
                        null,
                        configuration.Manual.Conversions[0].Form),
                    UnsupportedExceptionMessage =
                        BuildManualSettingConflictMessage(
                            configuration.Settings)
                };
            }

            var manual = ManualConvertMappingPlanner.Build(
                configuration.Manual.Conversions[0],
                mapperType,
                usedGeneratedMethodNames,
                cancellationToken);

            return mapping with
            {
                ManualMapping = new TypeMapperManualMappingModel(
                    manual.HelperMethodName,
                    manual.Form),
                HelperMethodDeclarations = manual.HelperMethodDeclaration is
                    { } helperMethodDeclaration
                    ? [helperMethodDeclaration]
                    : [],
                UnsupportedExceptionMessage = manual.UnsupportedMessage
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

        if (pair.Capabilities.DirectConstruction &&
            configuration.Settings.ConstructorSelection.Origin ==
                PairConfigurationSettingOrigin.Explicit)
        {
            return mapping with
            {
                UnsupportedExceptionMessage =
                    DirectConstructorSelectionConflictMessage
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
            configuration.Declarative.Members,
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

        var constructorSelection =
            effectiveSettings.ConstructorSelection;
        var resultPolicy = configuration.Declarative.ResultPolicies.IsEmpty
            ? (ResultPolicyConfigurationModel?)null
            : configuration.Declarative.ResultPolicies[0];

        TypeMapperMappingModel BuildFlatMapping(
            ConventionMemberMappingPlan memberMappings)
        {
            if (resultPolicy is { } configuredResultPolicy)
            {
                if (configuredResultPolicy.Kind is
                    ResultPolicyKind.ConstructUsing or
                    ResultPolicyKind.ResolveUsing)
                {
                    var runtimeResult = RuntimeResultMappingPlanner.Build(
                            configuredResultPolicy,
                            mapping,
                            memberMappings,
                            mapperType,
                            usedGeneratedMethodNames,
                            cancellationToken);

                    return mapping with
                    {
                        ControlFlow = runtimeResult.ControlFlow,
                        HelperMethodDeclarations =
                            runtimeResult.HelperMethodDeclarations,
                        UnsupportedExceptionMessage =
                            runtimeResult.UnsupportedMessage
                    };
                }

                if (!pair.Capabilities.StructuredConstruction ||
                    destinationPlan.MemberType is not
                    INamedTypeSymbol structuredDestination)
                {
                    return mapping with
                    {
                        UnsupportedExceptionMessage =
                            "The configured structured result callback " +
                            "requires a " +
                            "structured destination type."
                    };
                }

                var structuredConstruct =
                    StructuredConstructMappingPlanner.Build(
                        configuredResultPolicy,
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

            constructorMapping =
                ConventionConstructorMappingPlanner.Build(
                    declarativeSourceType,
                    destinationPlan.MemberType,
                    memberMappings.BuildConstructorInitializationPlan(
                        replacement: false),
                    pair.Capabilities,
                    constructorSelection,
                    compilation,
                    mapperType,
                    nonNullSourceName,
                    cancellationToken);

            if (constructorMapping is null)
            {
                createUnsupportedMessage = constructorSelection is null
                    ? InvalidConstructorSelectionMessage
                    : ConventionConstructionUnavailableMessage;
            }

            return mapping with
            {
                CreateConstructor = constructorMapping?.Constructor,
                CreateMemberMappings =
                    constructorMapping?.CreateMemberMappings ??
                    memberMappings.Create,
                CreatePostMemberMappings =
                    constructorMapping?.CreatePostMemberMappings ??
                    [],
                UpdateMemberMappings = memberMappings.Update,
                CreateUnsupportedExceptionMessage =
                    createUnsupportedMessage
            };
        }

        if (members.ControlFlow is not { } membersControlFlow)
        {
            return DeclarativeDependencyGraphOptimizer.Optimize(
                BuildFlatMapping(members.Plan),
                mapperType);
        }

        return DeclarativeDependencyGraphOptimizer.Optimize(
            MembersControlFlowMappingPlanner.Build(
                membersControlFlow,
                mapping,
                compilation,
                mapperType,
                resultPolicy?.Kind is
                    ResultPolicyKind.ConstructUsing or
                    ResultPolicyKind.ResolveUsing,
                BuildFlatMapping,
                cancellationToken),
            mapperType);
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
            SourceRuntimeTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedRuntimeTypeName(
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
            DestinationRuntimeTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedRuntimeTypeName(
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
            CreateDirectExpression: null,
            UpdateDirectExpression: null,
            CreateFactory: null,
            CreateConstructor: null,
            UpdateKind: destinationPlan.UpdateKind,
            CreateMemberMappings: [],
            CreatePostMemberMappings: [],
            UpdateMemberMappings: []);
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
        var memberType = DestinationCapabilityPolicy
            .GetNormalizedDestinationType(
                destinationType,
                compilation)
            .WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        var updateKind = memberType.IsReferenceType
            ? TypeMapperUpdateKind.Reference
            : memberType.TypeKind switch
        {
            TypeKind.Struct or TypeKind.Enum =>
                isNullableValue
                    ? TypeMapperUpdateKind.NullableValue
                    : TypeMapperUpdateKind.Value,
            _ => TypeMapperUpdateKind.Unsupported
        };

        return new DestinationPlan(memberType, updateKind);
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
                settings.ConstructorSelection,
                ConstructorSelectionValue.Default),
            GetSettingOrDefault(
                settings.MemberSelection,
                MemberSelectionValue.Default),
            GetSettingOrDefault(
                settings.UnmappedMemberValidation,
                UnmappedMemberValidationValue.Default));
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

    private static bool RequiresCreateMethod(
        TypeMapperMappingModel mapping,
        EffectiveMappingSettings settings)
    {
        if (mapping.UnsupportedExceptionMessage is not null ||
            mapping.ManualMapping is not null ||
            !settings.IsMappingModeValid ||
            !settings.IsNullSourceHandlingValid)
        {
            return false;
        }

        return settings.SupportsCreate ||
               mapping.DestinationCanBeNull &&
               settings.SupportsUpdate &&
               settings.IsNullDestinationHandlingValid &&
               settings.NullDestinationHandling ==
                   NullDestinationHandlingValue.Create;
    }

    private static bool CreatePathNeedsOperationParameter(
        TypeMapperMappingModel mapping)
    {
        return mapping.CreateUnsupportedExceptionMessage is not null ||
               mapping.ControlFlow is { } controlFlow &&
               ContainsGeneratedCreateFailure(controlFlow.CreateRoot) ||
               mapping.PostMemberControlFlow is { } postControlFlow &&
               ContainsGeneratedPostFailure(postControlFlow);
    }

    private static bool ContainsGeneratedCreateFailure(
        TypeMapperControlFlowNode node)
    {
        if (node.ThrowUsesCurrentMappingOperation)
        {
            return true;
        }

        if (node.Leaf is { } leaf)
        {
            return leaf.UnsupportedExceptionMessage is not null ||
                   leaf.CreateUnsupportedExceptionMessage is not null ||
                   leaf.CreateDirectExpression is null &&
                   leaf.CreateFactory is null &&
                   leaf.CreateConstructor is null;
        }

        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            return ContainsGeneratedCreateFailure(evaluationContinuation);
        }

        if (node.SwitchExpression is not null)
        {
            return node.SwitchSections.Any(static section =>
                       ContainsGeneratedCreateFailure(section.Branch)) ||
                   node.SwitchContinuation is { } continuation &&
                   ContainsGeneratedCreateFailure(continuation);
        }

        if (node.Condition is not null)
        {
            return ContainsGeneratedCreateFailure(node.WhenTrue!) ||
                   ContainsGeneratedCreateFailure(node.WhenFalse!);
        }

        return false;
    }

    private static bool ContainsGeneratedPostFailure(
        TypeMapperMemberControlFlowNode node)
    {
        if (node.ThrowUsesCurrentMappingOperation ||
            node.UnsupportedExceptionMessage is not null)
        {
            return true;
        }

        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            return ContainsGeneratedPostFailure(evaluationContinuation);
        }

        if (node.SwitchExpression is not null)
        {
            return node.SwitchSections.Any(static section =>
                       ContainsGeneratedPostFailure(section.Branch)) ||
                   node.SwitchContinuation is { } continuation &&
                   ContainsGeneratedPostFailure(continuation);
        }

        if (node.Condition is not null)
        {
            return ContainsGeneratedPostFailure(node.WhenTrue!) ||
                   ContainsGeneratedPostFailure(node.WhenFalse!);
        }

        return false;
    }

    private static bool RequiresUpdateMethod(
        TypeMapperMappingModel mapping,
        EffectiveMappingSettings settings)
    {
        return mapping.UnsupportedExceptionMessage is null &&
               mapping.ManualMapping is null &&
               settings.IsMappingModeValid &&
               settings.SupportsUpdate &&
               settings.IsNullSourceHandlingValid &&
               settings.IsNullDestinationHandlingValid;
    }

    private static bool HasExplicitManualSettingConflict(
        PairConfigurationSettings settings)
    {
        return settings.NullSourceHandling.Origin ==
                   PairConfigurationSettingOrigin.Explicit ||
               settings.NullDestinationHandling.Origin ==
                   PairConfigurationSettingOrigin.Explicit ||
               settings.ConstructorSelection.Origin ==
                   PairConfigurationSettingOrigin.Explicit ||
               settings.MemberSelection.Origin ==
                   PairConfigurationSettingOrigin.Explicit ||
               settings.UnmappedMemberValidation.Origin ==
                   PairConfigurationSettingOrigin.Explicit;
    }

    private static string BuildManualSettingConflictMessage(
        PairConfigurationSettings settings)
    {
        var names = ImmutableArray.CreateBuilder<string>();

        AddExplicitSetting(
            names,
            settings.NullSourceHandling,
            nameof(settings.NullSourceHandling));
        AddExplicitSetting(
            names,
            settings.NullDestinationHandling,
            nameof(settings.NullDestinationHandling));
        AddExplicitSetting(
            names,
            settings.ConstructorSelection,
            nameof(settings.ConstructorSelection));
        AddExplicitSetting(
            names,
            settings.MemberSelection,
            nameof(settings.MemberSelection));
        AddExplicitSetting(
            names,
            settings.UnmappedMemberValidation,
            nameof(settings.UnmappedMemberValidation));

        return "The following map-level settings are not applicable to " +
               "Convert: " + string.Join(", ", names) + ".";
    }

    private static void AddExplicitSetting<TValue>(
        ImmutableArray<string>.Builder names,
        PairConfigurationSetting<TValue> setting,
        string name)
        where TValue : struct, Enum
    {
        if (setting.Origin == PairConfigurationSettingOrigin.Explicit)
        {
            names.Add(name);
        }
    }

    private static string BuildConfiguredPlanConflictMessage(
        PairConfigurationConflict conflicts)
    {
        var reasons = ImmutableArray.CreateBuilder<string>();

        AddConflictReason(
            reasons,
            conflicts,
            PairConfigurationConflict.DuplicateResultPolicy,
            "more than one result callback is configured");
        AddConflictReason(
            reasons,
            conflicts,
            PairConfigurationConflict.DuplicateMembers,
            "more than one Members callback is configured");
        AddConflictReason(
            reasons,
            conflicts,
            PairConfigurationConflict.DuplicateConvert,
            "more than one Convert callback is configured");
        AddConflictReason(
            reasons,
            conflicts,
            PairConfigurationConflict.MixedManualAndDeclarative,
            "Convert is combined with a result callback or Members");
        AddConflictReason(
            reasons,
            conflicts,
            PairConfigurationConflict.DuplicateIncludeBase,
            "more than one IncludeBase call is configured");
        AddConflictReason(
            reasons,
            conflicts,
            PairConfigurationConflict.MissingBaseConfiguration,
            "the selected base mapper configuration is unavailable");
        AddConflictReason(
            reasons,
            conflicts,
            PairConfigurationConflict.MissingBasePair,
            "the selected base mapper does not configure the requested pair");
        AddConflictReason(
            reasons,
            conflicts,
            PairConfigurationConflict.IncompatibleBasePair,
            "the IncludeBase pair is incompatible with the current pair");
        AddConflictReason(
            reasons,
            conflicts,
            PairConfigurationConflict.InaccessibleInheritedPlan,
            "an inherited callback is inaccessible from the generated mapper");
        AddConflictReason(
            reasons,
            conflicts,
            PairConfigurationConflict.CyclicIncludeBase,
            "IncludeBase contains a cycle");

        return "The configured mapping plan is invalid: " +
               string.Join("; ", reasons) + ".";
    }

    private static void AddConflictReason(
        ImmutableArray<string>.Builder reasons,
        PairConfigurationConflict conflicts,
        PairConfigurationConflict conflict,
        string reason)
    {
        if ((conflicts & conflict) != 0)
        {
            reasons.Add(reason);
        }
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
        string Source)
    {
        public string HintName => GeneratedSourceHintName.Create(
            "TypeMapper",
            HintNameHelper.ToHintNamePart(StableIdentity));
    }

    private readonly record struct DestinationPlan(
        ITypeSymbol MemberType,
        TypeMapperUpdateKind UpdateKind);

    private readonly record struct OrderedMapping(
        int Position,
        TypeMapperMappingModel Mapping);
}
