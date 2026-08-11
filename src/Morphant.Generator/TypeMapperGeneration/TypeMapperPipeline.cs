using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.Incrementality;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TypeMapperPipeline
{
    private const string InvalidBaseConfigurationMessage =
        "The mapper inheritance chain contains a base mapper whose " +
        "configuration cannot be composed.";

    private const string UnsupportedMapperBuilderFlowMessage =
        "The mapper builder flow cannot be analyzed.";

    private const string UnsupportedMappingBuilderFlowMessage =
        "The mapping builder flow cannot be analyzed.";

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
        Register(
            context,
            compilationContext,
            assemblySettings,
            MapperContractPipeline.Build(
                mapperConfigurations,
                compilationContext));
    }

    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValueProvider<MappingSettings> assemblySettings,
        IncrementalValuesProvider<MapperContractAnalysis> contractAnalyses)
    {
        var models = contractAnalyses
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
                MapperContractAnalysis Analysis,
                CompilationContext Context
            ) Input,
            MappingSettings AssemblySettings
        ) source,
        CancellationToken cancellationToken)
    {
        var ((analysis, context), assemblySettings) = source;
        var configuration = analysis.Configuration;
        var configureSyntax = configuration.MappingPairs.ConfigureSyntax;
        var mapperDeclaration =
            configuration.Declaration.AttributedDeclaration;
        var mapperType = configuration.Declaration.MapperType;

        if (!configuration.Declaration.CanGenerateExecutableArtifact ||
            !IsSupportedAccessibility(mapperType.DeclaredAccessibility) ||
            context.Compilation is not CSharpCompilation compilation)
        {
            return null;
        }

        var mappings = BuildMappings(
            analysis,
            assemblySettings,
            compilation,
            mapperType,
            cancellationToken);

        if (mappings.Models.IsDefaultOrEmpty)
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
            mappings.Models,
            configureSyntax.DescendantNodes()
                .OfType<QueryExpressionSyntax>()
                .Any());
        model = TypeMapperTransferValidator.Validate(
            model,
            mappings.Policies,
            compilation,
            configureSyntax.SyntaxTree.Options as CSharpParseOptions,
            cancellationToken);

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

    private static TypeMapperMappingsBuildResult BuildMappings(
        MapperContractAnalysis analysis,
        MappingSettings assemblySettings,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var configuration = analysis.Configuration;
        var usedGeneratedMethodNames = BuildUsedGeneratedMethodNames(
            mapperType);
        var mappings = ImmutableArray.CreateBuilder<OrderedMapping>(
            configuration.Pairs.Length +
            configuration.MappingPairs.UnsupportedPairs.Length);
        var configuredPairKeys = new HashSet<MappingIdentityKey>();

        foreach (var pairConfiguration in configuration.Pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (analysis.HasGeneratedConflict(
                    pairConfiguration.Pair.Identity))
            {
                continue;
            }

            if (analysis.Excludes(pairConfiguration.Pair.Identity))
            {
                continue;
            }

            configuredPairKeys.Add(MappingIdentityKey.Create(
                pairConfiguration.Pair));

            if (TryGetConfigurationFlowFailure(
                    configuration,
                    pairConfiguration.Pair.Identity,
                    out var flowFailure))
            {
                mappings.Add(new OrderedMapping(
                    pairConfiguration.Pair.Registration.Syntax.SpanStart,
                    BuildFailedMapping(
                        pairConfiguration.Pair.Registration,
                        pairConfiguration.Pair.Identity,
                        compilation,
                        mapperType,
                        flowFailure.Reason,
                        flowFailure.Message,
                        flowFailure.OriginKind),
                    TransferredCodePolicy.Empty));
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

            mapping = MappingCompletenessObservationBuilder.Attach(
                mapping,
                pairConfiguration,
                compilation,
                mapperType,
                cancellationToken);

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

            var transferPolicy =
                TransferredCodePolicy.Build(pairConfiguration);

            mappings.Add(new OrderedMapping(
                pairConfiguration.Pair.Registration.Syntax.SpanStart,
                mapping with
                {
                    EffectiveSettings = effectiveSettings,
                    CreateImplMethodName = createMethodName,
                    UpdateImplMethodName = updateMethodName,
                    CreateImplUsesOperation = createImplUsesOperation,
                    RequiresUnsafeContext =
                        transferPolicy.RequiresUnsafeContext
                },
                transferPolicy));
        }

        foreach (var pair in configuration.MappingPairs.Pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (configuredPairKeys.Contains(
                    MappingIdentityKey.Create(pair)) ||
                analysis.Excludes(pair.Identity) ||
                !TryGetConfigurationFlowFailure(
                    configuration,
                    pair.Identity,
                    out var flowFailure))
            {
                continue;
            }

            mappings.Add(new OrderedMapping(
                pair.Registration.Syntax.SpanStart,
                BuildFailedMapping(
                    pair.Registration,
                    pair.Identity,
                    compilation,
                    mapperType,
                    flowFailure.Reason,
                    flowFailure.Message,
                    flowFailure.OriginKind),
                TransferredCodePolicy.Empty));
        }

        foreach (var unsupportedPair in
                 configuration.MappingPairs.UnsupportedPairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (analysis.HasGeneratedConflict(unsupportedPair.Identity))
            {
                continue;
            }

            if (analysis.Excludes(unsupportedPair.Identity))
            {
                continue;
            }

            mappings.Add(new OrderedMapping(
                unsupportedPair.Registration.Syntax.SpanStart,
                BuildUnsupportedMapping(
                    unsupportedPair,
                    compilation,
                    mapperType),
                TransferredCodePolicy.Empty));
        }

        var orderedMappings = mappings
            .OrderBy(static mapping => mapping.Position)
            .ToImmutableArray();

        return new TypeMapperMappingsBuildResult(
            orderedMappings
                .Select(static mapping => mapping.Mapping)
                .ToImmutableArray(),
            orderedMappings
                .Select(static mapping => mapping.Policy)
                .ToImmutableArray());
    }

    private static TypeMapperMappingModel BuildUnsupportedMapping(
        UnsupportedMappingPairModel pair,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType)
    {
        return BuildFailedMapping(
            pair.Registration,
            pair.Identity,
            compilation,
            mapperType,
            MappingFailureReason.UnsupportedMappingContract,
            BuildUnsupportedMappingReason(pair),
            MappingObservationOriginKind.Registration);
    }

    private static TypeMapperMappingModel BuildFailedMapping(
        MappingPairRegistrationModel registration,
        MappingPairIdentity identity,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        MappingFailureReason failureReason,
        string failureMessage,
        MappingObservationOriginKind originKind)
    {
        var declarativeSourceType =
            MappingTypeNormalization.NormalizeDeclarativeSource(
                registration.SourceType,
                compilation);
        var previousDestinationType =
            MappingTypeNormalization.NormalizePreviousDestination(
                registration.DestinationType,
                compilation);
        var nonNullSourceName = BuildNonNullSourceName(
            registration.SourceType,
            mapperType);
        var usedLocalNames =
            UserResultMappingPlanner.BuildUsedLocalNames(mapperType);
        usedLocalNames.Add(nonNullSourceName);

        return new TypeMapperMappingModel(
            SourceTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    registration.SourceType),
            SourceRuntimeTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedRuntimeTypeName(
                    registration.SourceType),
            MaybeNullSourceTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedMaybeNullTypeName(
                    registration.SourceType),
            NonNullSourceTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    declarativeSourceType),
            NonNullSourceName: nonNullSourceName,
            DestinationTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    registration.DestinationType),
            DestinationRuntimeTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedRuntimeTypeName(
                    registration.DestinationType),
            MaybeNullDestinationTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedMaybeNullTypeName(
                    registration.DestinationType),
            NonNullDestinationTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    previousDestinationType),
            ResultLocalName: AllocateName("result", usedLocalNames),
            AnalysisContext: new MappingAnalysisContext(
                registration,
                identity,
                mapperType),
            SourceCanBeNull: CanBeNull(registration.SourceType),
            SourceIsNullableValue:
                MappingTypeNormalization.IsNullableValue(
                    registration.SourceType),
            DestinationCanBeNull: CanBeNull(registration.DestinationType),
            CreateDirectExpression: null,
            UpdateDirectExpression: null,
            CreateFactory: null,
            CreateConstructor: null,
            UpdateKind: TypeMapperUpdateKind.Unsupported,
            CreateMemberMappings: [],
            CreatePostMemberMappings: [],
            UpdateMemberMappings: [],
            Failure: MappingFailureObservation.Create(
                new MappingAnalysisContext(
                    registration,
                    identity,
                    mapperType),
                failureReason,
                failureMessage,
                originKind,
                MappingAffectedPath.All(
                    MappingPlanPhase.Configuration)));
    }

    private static bool TryGetConfigurationFlowFailure(
        MapperPairConfigurationModel configuration,
        MappingPairIdentity identity,
        out ConfigurationFlowFailure failure)
    {
        if (configuration.HasInvalidBaseConfiguration)
        {
            failure = new ConfigurationFlowFailure(
                MappingFailureReason.InvalidBaseConfiguration,
                InvalidBaseConfigurationMessage,
                MappingObservationOriginKind.MapperConfiguration);
            return true;
        }

        if (configuration.FlowBreaks.Any(static flowBreak =>
                flowBreak.Kind == BuilderFlowBreakKind.Mapper))
        {
            failure = new ConfigurationFlowFailure(
                MappingFailureReason.UnsupportedMapperBuilderFlow,
                UnsupportedMapperBuilderFlowMessage,
                MappingObservationOriginKind.MapperConfiguration);
            return true;
        }

        if (configuration.FlowBreaks.Any(flowBreak =>
                flowBreak.Kind == BuilderFlowBreakKind.Mapping &&
                flowBreak.Registration is { } registration &&
                IsIdentity(registration, identity) &&
                !IsDiscardedDuplicate(configuration, registration)))
        {
            failure = new ConfigurationFlowFailure(
                MappingFailureReason.UnsupportedMappingBuilderFlow,
                UnsupportedMappingBuilderFlowMessage,
                MappingObservationOriginKind.Registration);
            return true;
        }

        failure = default;
        return false;
    }

    private static bool IsIdentity(
        MappingPairRegistrationModel registration,
        MappingPairIdentity identity)
    {
        var registrationIdentity = new MappingPairIdentity(
            MappingTypeIdentityPolicy.Create(registration.SourceType),
            MappingTypeIdentityPolicy.Create(registration.DestinationType));

        return StringComparer.Ordinal.Equals(
                   registrationIdentity.Source.Key,
                   identity.Source.Key) &&
               StringComparer.Ordinal.Equals(
                   registrationIdentity.Destination.Key,
                   identity.Destination.Key);
    }

    private static bool IsDiscardedDuplicate(
        MapperPairConfigurationModel configuration,
        MappingPairRegistrationModel registration)
    {
        return configuration.SurfaceMappingPairs.Any(model =>
            model.DuplicateRegistrations.Any(duplicate =>
                duplicate.Registration.Syntax.SyntaxTree ==
                    registration.Syntax.SyntaxTree &&
                duplicate.Registration.Syntax.Span ==
                    registration.Syntax.Span));
    }

    private static string BuildUnsupportedMappingReason(
        UnsupportedMappingPairModel pair)
    {
        return string.Join(
            " ",
            pair.UnsupportedRoots.Select(static root =>
                $"The {GetRoleName(root.Role)} type " +
                $"'{MapperContractDisplay.CreateType(root.Type)}' is not " +
                $"supported as a mapping root because it is {root.Reason}."));
    }

    private static string GetRoleName(MappingTypeRole role)
    {
        return role == MappingTypeRole.Source
            ? "source"
            : "destination";
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
                Failure = MappingFailureObservation.Create(
                    mapping.AnalysisContext,
                    MappingFailureReason.InvalidPairConfiguration,
                    BuildConfiguredPlanConflictMessage(
                        configuration.Conflicts),
                    MappingObservationOriginKind.Registration,
                    MappingAffectedPath.All(
                        MappingPlanPhase.Configuration))
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
                    Failure = MappingFailureObservation.Create(
                        mapping.AnalysisContext,
                        MappingFailureReason.InvalidManualSetting,
                        BuildManualSettingConflictMessage(
                            configuration.Settings),
                        MappingObservationOriginKind.Setting,
                        MappingAffectedPath.All(
                            MappingPlanPhase.Configuration),
                        FindFirstExplicitManualSettingSyntax(
                            configuration.Settings))
                };
            }

            var manual = ManualConvertMappingPlanner.Build(
                configuration.Manual.Conversions[0],
                mapping.AnalysisContext,
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
                Failure = manual.Failure
            };
        }

        if (!effectiveSettings.IsMemberSelectionValid)
        {
            var failure = MappingFailureObservation.Create(
                mapping.AnalysisContext,
                MappingFailureReason.InvalidSetting,
                InvalidMemberSelectionMessage,
                MappingObservationOriginKind.Setting,
                MappingAffectedPath.All(
                    MappingPlanPhase.Configuration),
                configuration.Settings.MemberSelection.Syntax);

            return mapping with
            {
                CreateOperationFailure = failure,
                UpdateOperationFailure = failure
            };
        }

        if (pair.Capabilities.DirectConstruction &&
            configuration.Settings.ConstructorSelection.Origin ==
                PairConfigurationSettingOrigin.Explicit)
        {
            return mapping with
            {
                Failure = MappingFailureObservation.Create(
                    mapping.AnalysisContext,
                    MappingFailureReason.InapplicableSetting,
                    DirectConstructorSelectionConflictMessage,
                    MappingObservationOriginKind.Setting,
                    MappingAffectedPath.All(
                        MappingPlanPhase.Configuration),
                    configuration.Settings.ConstructorSelection.Syntax)
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

        if (members.Failure is { } membersFailure)
        {
            return mapping with
            {
                Failure = membersFailure
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
            var observedMapping = mapping with
            {
                MemberObservation = memberMappings.Observation,
                NestedObservations = memberMappings.Observation
                    .NestedMappings.IsDefault
                        ? []
                        : memberMappings.Observation.NestedMappings
            };

            if (resultPolicy is { } configuredResultPolicy)
            {
                if (configuredResultPolicy.Kind is
                    ResultPolicyKind.ConstructUsing or
                    ResultPolicyKind.ResolveUsing)
                {
                    var runtimeResult = RuntimeResultMappingPlanner.Build(
                        configuredResultPolicy,
                        observedMapping,
                        memberMappings,
                        mapperType,
                        usedGeneratedMethodNames,
                        cancellationToken);

                    return observedMapping with
                    {
                        ControlFlow = runtimeResult.ControlFlow,
                        HelperMethodDeclarations =
                            runtimeResult.HelperMethodDeclarations,
                        Failure = runtimeResult.Failure
                    };
                }

                if (!pair.Capabilities.StructuredConstruction ||
                    destinationPlan.MemberType is not
                    INamedTypeSymbol structuredDestination)
                {
                    return observedMapping with
                    {
                        Failure = MappingFailureObservation.Create(
                            observedMapping.AnalysisContext,
                            MappingFailureReason
                                .StructuredResultRequiresDestination,
                            "The configured structured result callback " +
                            "requires a structured destination type.",
                            MappingObservationOriginKind.Callback,
                            new MappingAffectedPath(
                                configuredResultPolicy.Kind ==
                                    ResultPolicyKind.Construct
                                        ? MappingExecutionPathSet.NoPrevious
                                        : MappingExecutionPathSet.All,
                                MappingPlanPhase.ResultSelection),
                            configuredResultPolicy.Invocation,
                            configuredResultPolicy.Expression
                                .DeclaringMapperType)
                    };
                }

                var structuredConstruct =
                    StructuredConstructMappingPlanner.Build(
                        configuredResultPolicy,
                        observedMapping,
                        declarativeSourceType,
                        structuredDestination,
                        pair.Capabilities,
                        memberMappings,
                        constructorSelection,
                        compilation,
                        mapperType,
                        usedGeneratedMethodNames,
                        cancellationToken);

                return observedMapping with
                {
                    ControlFlow = structuredConstruct.ControlFlow,
                    HelperMethodDeclarations =
                        structuredConstruct.HelperMethodDeclarations,
                    Failure = structuredConstruct.Failure
                };
            }

            ConventionConstructorMappingPlan? constructorMapping = null;
            MappingFailureObservation? createFailure = null;

            var constructorPlanning =
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
            constructorMapping = constructorPlanning.Plan;
            var constructorObservation =
                constructorPlanning.Observation with
                {
                    StrategyOrigin = configuration.Settings
                        .ConstructorSelection.Syntax
                };

            if (constructorMapping is null)
            {
                createFailure = MappingFailureObservation.Create(
                    observedMapping.AnalysisContext,
                    constructorSelection is null
                        ? MappingFailureReason.InvalidSetting
                        : MappingFailureReason.ConstructorSelectionFailed,
                    constructorSelection is null
                        ? InvalidConstructorSelectionMessage
                        : ConventionConstructionUnavailableMessage,
                    constructorSelection is null
                        ? MappingObservationOriginKind.Setting
                        : MappingObservationOriginKind.Convention,
                    MappingAffectedPath.NoPrevious(
                        MappingPlanPhase.Construction),
                    configuration.Settings.ConstructorSelection.Syntax);
            }

            return observedMapping with
            {
                CreateConstructor = constructorMapping?.Constructor,
                CreateMemberMappings =
                    constructorMapping?.CreateMemberMappings ??
                    memberMappings.Create,
                CreatePostMemberMappings =
                    constructorMapping?.CreatePostMemberMappings ??
                    [],
                UpdateMemberMappings = memberMappings.Update,
                CreateFailure = createFailure,
                ConstructorObservation = constructorObservation
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
            AnalysisContext: new MappingAnalysisContext(
                pair.Registration,
                pair.Identity,
                mapperType),
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
        if (mapping.Failure is not null ||
            mapping.ManualMapping is not null ||
            !settings.IsMappingModeValid ||
            !settings.IsNullSourceHandlingValid)
        {
            return false;
        }

        var createRequiresImplementation =
            settings.SupportsCreate &&
            mapping.CreateOperationFailure is null;
        var updateWithoutPreviousRequiresImplementation =
            mapping.DestinationCanBeNull &&
            settings.SupportsUpdate &&
            mapping.UpdateOperationFailure is null &&
            settings.IsNullDestinationHandlingValid &&
            settings.NullDestinationHandling ==
                NullDestinationHandlingValue.Create;

        return createRequiresImplementation ||
               updateWithoutPreviousRequiresImplementation;
    }

    private static bool CreatePathNeedsOperationParameter(
        TypeMapperMappingModel mapping)
    {
        return mapping.CreateFailure is not null ||
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
            return leaf.Failure is not null ||
                   leaf.CreateFailure is not null ||
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
            node.Failure is not null)
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
        return mapping.Failure is null &&
               mapping.ManualMapping is null &&
               mapping.UpdateOperationFailure is null &&
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

    private static SyntaxNode? FindFirstExplicitManualSettingSyntax(
        PairConfigurationSettings settings)
    {
        if (settings.NullSourceHandling.Origin ==
            PairConfigurationSettingOrigin.Explicit)
        {
            return settings.NullSourceHandling.Syntax;
        }

        if (settings.NullDestinationHandling.Origin ==
            PairConfigurationSettingOrigin.Explicit)
        {
            return settings.NullDestinationHandling.Syntax;
        }

        if (settings.ConstructorSelection.Origin ==
            PairConfigurationSettingOrigin.Explicit)
        {
            return settings.ConstructorSelection.Syntax;
        }

        if (settings.MemberSelection.Origin ==
            PairConfigurationSettingOrigin.Explicit)
        {
            return settings.MemberSelection.Syntax;
        }

        return settings.UnmappedMemberValidation.Syntax;
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
            PairConfigurationConflict.InvalidBasePair,
            "the included mapping pair is invalid");
        AddConflictReason(
            reasons,
            conflicts,
            PairConfigurationConflict.InaccessibleInheritedPlan,
            "an inherited callback is inaccessible from the generated mapper");

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
        TypeMapperMappingModel Mapping,
        TransferredCodePolicy Policy);

    private readonly record struct TypeMapperMappingsBuildResult(
        ImmutableArray<TypeMapperMappingModel> Models,
        ImmutableArray<TransferredCodePolicy> Policies);

    private readonly record struct ConfigurationFlowFailure(
        MappingFailureReason Reason,
        string Message,
        MappingObservationOriginKind OriginKind);

    private readonly record struct MappingIdentityKey(
        string Source,
        string Destination)
    {
        public static MappingIdentityKey Create(MappingPairModel pair) =>
            new(pair.Identity.Source.Key, pair.Identity.Destination.Key);
    }
}
