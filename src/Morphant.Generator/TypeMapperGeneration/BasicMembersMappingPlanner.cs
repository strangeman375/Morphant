using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class BasicMembersMappingPlanner
{
    private const string UnsupportedMembersMessage =
        "The configured Members callback cannot be represented by the " +
        "supported declarative grammar.";

    private const string AutomaticMemberUnavailableMessage =
        "A configured Auto member cannot be mapped by convention.";

    public static BasicMembersMappingResult Build(
        ImmutableArray<MembersConfigurationModel> configurations,
        MemberSelectionValue memberSelection,
        TypeMapperMappingModel mapping,
        ConventionMemberMappingPlan convention,
        ITypeSymbol destination,
        MappingPairCapabilities capabilities,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (configurations.Length <= 1)
        {
            return BuildSingle(
                configurations.IsEmpty ? null : configurations[0],
                memberSelection,
                mapping,
                convention,
                destination,
                capabilities,
                compilation,
                mapperType,
                cancellationToken);
        }

        var results = ImmutableArray.CreateBuilder<BasicMembersMappingResult>(
            configurations.Length);

        foreach (var configuration in configurations)
        {
            var result = BuildSingle(
                configuration,
                MemberSelectionValue.Explicit,
                mapping,
                convention,
                destination,
                capabilities,
                compilation,
                mapperType,
                cancellationToken);

            if (result.Failure is not null ||
                result.ControlFlow is not null)
            {
                return result.Failure is { } failure
                    ? BasicMembersMappingResult.Unsupported(failure)
                    : BasicMembersMappingResult.Unsupported(
                        BuildFailure(
                            mapping,
                            configuration,
                            MappingFailureReason
                                .UnsupportedStructuredCallback,
                            UnsupportedMembersMessage));
            }

            results.Add(result);
        }

        return new BasicMembersMappingResult(
            MergePlans(
                results.Select(static result => result.Plan),
                memberSelection,
                convention,
                destination,
                capabilities,
                compilation,
                cancellationToken),
            ControlFlow: null,
            Failure: null);
    }

    private static BasicMembersMappingResult BuildSingle(
        MembersConfigurationModel? configuration,
        MemberSelectionValue memberSelection,
        TypeMapperMappingModel mapping,
        ConventionMemberMappingPlan convention,
        ITypeSymbol destination,
        MappingPairCapabilities capabilities,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (configuration is null)
        {
            if (memberSelection == MemberSelectionValue.Auto)
            {
                return new BasicMembersMappingResult(
                    convention,
                    ControlFlow: null,
                    Failure: null);
            }

            var emptyCreate =
                ImmutableArray<TypeMapperMemberMappingModel>.Empty;

            return new BasicMembersMappingResult(
                new ConventionMemberMappingPlan(
                    emptyCreate,
                    [],
                    emptyCreate,
                    [],
                    [],
                    convention.Observation with
                    {
                        Rules = [],
                        RequiredObligations =
                            ConventionMemberMappingPlanner
                                .FindUnmappedRequiredMembers(
                                    destination,
                                    emptyCreate,
                                    cancellationToken)
                    }),
                ControlFlow: null,
                Failure: null);
        }

        var configured = configuration.Value;

        if (configured.Expression.Syntax is not
                LambdaExpressionSyntax lambda ||
            !TryGetLambdaParameters(
                lambda,
                configured.Expression.SemanticModel,
                configured.Form,
                cancellationToken,
                out var sourceParameter,
                out var previousParameter,
                out var resultParameter,
                out var contextParameter) ||
            !DeclarativeContextUsagePolicy.IsSupported(
                lambda,
                contextParameter,
                configured.Expression.SemanticModel,
                cancellationToken) ||
            !DeclarativeDeferredCapturePolicy.IsSupported(
                lambda,
                previousParameter,
                resultParameter,
                contextParameter,
                configured.Expression.SemanticModel,
                cancellationToken))
        {
            return BasicMembersMappingResult.Unsupported(
                BuildFailure(
                    mapping,
                    configured,
                    MappingFailureReason.UnsupportedStructuredCallback,
                    UnsupportedMembersMessage));
        }

        var controlFlowResult = DeclarativeControlFlowPlanner.Build(
            lambda,
            configured.Expression.SemanticModel,
            cancellationToken);

        if (controlFlowResult is UnsupportedDeclarativeControlFlow
            unsupportedControlFlow)
        {
            return BasicMembersMappingResult.Unsupported(
                BuildFailure(
                    mapping,
                    configured,
                    MappingFailureReason.UnsupportedStructuredSyntax,
                    unsupportedControlFlow.Message));
        }

        if (controlFlowResult is not DeclarativeControlFlowProgram
            controlFlow)
        {
            return BasicMembersMappingResult.Unsupported(
                BuildFailure(
                    mapping,
                    configured,
                    MappingFailureReason.UnsupportedStructuredCallback,
                    UnsupportedMembersMessage));
        }

        var writableMembers =
            ConventionMemberMappingPlanner.BuildWritableMembers(
                destination,
                capabilities,
                compilation,
                cancellationToken);
        var writableMembersByName = writableMembers.ToDictionary(
            static member => member.Name,
            StringComparer.Ordinal);
        var conventionCreateByName = convention.Create.ToDictionary(
            static member => member.DestinationMemberName,
            StringComparer.Ordinal);
        var conventionUpdateByName =
            convention.Update.ToDictionary(
                static member => member.DestinationMemberName,
                StringComparer.Ordinal);
        var conventionCreatePostByName =
            convention.CreatePost.ToDictionary(
                static member => member.DestinationMemberName,
                StringComparer.Ordinal);
        var runtimeLocalInitializers =
            controlFlow.RuntimeLocals.ToDictionary(
                local => controlFlow.RuntimeLocalPlaceholders.First(pair =>
                        StringComparer.Ordinal.Equals(
                            pair.Value,
                            local.PlaceholderName))
                    .Key,
                static local => local.Initializer,
                SymbolEqualityComparer.Default);
        var configuredDestination = TryGetConfiguredDestinationType(
            configured,
            cancellationToken);
        var leaves =
            new Dictionary<
                DeclarativeLeafSyntaxNode,
                ConventionMemberMappingPlan>();
        MappingFailureObservation? leafFailure = null;

        bool BuildLeaf(DeclarativeLeafSyntaxNode leaf)
        {
            if (leaf.DirectExpression is { } directExpression &&
                TryGetOmittedProducer(
                    directExpression,
                    out var omittedProducer))
            {
                var failure = BuildFailure(
                    mapping,
                    configured,
                    MappingFailureReason.TerminalNullMembers,
                    UnsupportedMembersMessage,
                    omittedProducer);
                var terminal = new StructuredTerminalObservation(
                    StructuredTerminalKind.NullMembers,
                    omittedProducer,
                    MappingAffectedPath.All(
                        MappingPlanPhase.Members) with
                    {
                        BranchOrigin = directExpression
                    },
                    leaf.TerminalAliases.IsDefault
                        ? []
                        : leaf.TerminalAliases);

                leaves.Add(
                    leaf,
                    new ConventionMemberMappingPlan(
                        [],
                        [],
                        [],
                        [],
                        [],
                        convention.Observation with
                        {
                            Rules = [],
                            RequiredObligations = [],
                            Terminals = [terminal],
                            PlanOrigin = directExpression
                        },
                        ConfiguredMemberNames: [],
                        Failure: failure));
                return true;
            }

            if (leaf.ObjectCreation is null ||
                !leaf.Arguments.IsEmpty ||
                !TryBuildLeafPlan(
                    leaf.MemberAssignments,
                    memberSelection,
                    mapping,
                    convention,
                    destination,
                    writableMembersByName,
                    conventionCreateByName,
                    conventionCreatePostByName,
                    conventionUpdateByName,
                    configured.Expression.SemanticModel,
                    mapperType,
                    sourceParameter,
                    previousParameter,
                    resultParameter,
                    contextParameter,
                    lambda,
                    configured.Expression.DeclaringMapperType,
                    configuredDestination,
                    controlFlow.RuntimeLocalPlaceholders,
                    runtimeLocalInitializers,
                    cancellationToken,
                    out var plan,
                    out leafFailure))
            {
                return false;
            }

            plan = plan with
            {
                Observation = plan.Observation with
                {
                    SourceDiscards = controlFlow.SourceDiscards
                        .Select(discard =>
                            new SourceDiscardObservation(
                                discard.Member,
                                discard.Statement,
                                configured.Expression))
                        .ToImmutableArray(),
                    PlanOrigin = leaf.ObjectCreation
                }
            };

            if (resultParameter is not null &&
                FindResultDependentPathOrigin(
                    controlFlow.Root,
                    leaf,
                    resultParameter,
                    configured.Expression.SemanticModel,
                    runtimeLocalInitializers,
                    cancellationToken) is { } pathDependency)
            {
                plan = plan with
                {
                    Observation = plan.Observation with
                    {
                        Rules = plan.Observation.Rules.Select(rule =>
                                rule.InvalidReason ==
                                    MemberRuleInvalidReason.None &&
                                rule.Origin is not
                                    (MemberRuleOrigin.Convention or
                                     MemberRuleOrigin.Ignore) &&
                                rule.Lifecycle.HasFlag(
                                    MemberLifecycleDependency.Creation) &&
                                (rule.IsRequired ||
                                 rule.Lifecycle.HasFlag(
                                     MemberLifecycleDependency.InitOnly))
                                    ? rule with
                                    {
                                        Lifecycle = rule.Lifecycle |
                                            MemberLifecycleDependency.Result,
                                        ResultDependencyOrigin =
                                            rule.ResultDependencyOrigin ??
                                            pathDependency
                                    }
                                    : rule)
                            .ToImmutableArray()
                    }
                };
            }

            leaves.Add(leaf, plan);
            return true;
        }

        foreach (var leaf in EnumerateLeaves(controlFlow.Root))
        {
            if (!BuildLeaf(leaf))
            {
                return BasicMembersMappingResult.Unsupported(
                    leafFailure ?? BuildFailure(
                        mapping,
                        configured,
                        MappingFailureReason.UnsupportedStructuredSyntax,
                        UnsupportedMembersMessage,
                        leaf.ObjectCreation ?? leaf.DirectExpression));
            }
        }

        var representativePlan = leaves.Values.First();
        var hasControlFlow =
            controlFlow.Root is not DeclarativeLeafSyntaxNode ||
            !controlFlow.RuntimeLocals.IsEmpty ||
            !controlFlow.BoundLocals.IsEmpty;

        return new BasicMembersMappingResult(
            representativePlan,
            hasControlFlow
                ? new MembersDeclarativeControlFlowPlan(
                    controlFlow,
                    leaves,
                    configured.Expression.SemanticModel,
                    mapperType,
                    sourceParameter,
                    previousParameter,
                    resultParameter,
                    contextParameter,
                    lambda,
                    runtimeLocalInitializers)
                : null,
            Failure: null);
    }

    private static bool TryBuildLeafPlan(
        ImmutableArray<DeclarativeMemberAssignmentSyntax> assignments,
        MemberSelectionValue memberSelection,
        TypeMapperMappingModel mapping,
        ConventionMemberMappingPlan convention,
        ITypeSymbol destination,
        IReadOnlyDictionary<string, ConventionWritableMember>
            writableMembersByName,
        IReadOnlyDictionary<string, TypeMapperMemberMappingModel>
            conventionCreateByName,
        IReadOnlyDictionary<string, TypeMapperMemberMappingModel>
            conventionCreatePostByName,
        IReadOnlyDictionary<string, TypeMapperMemberMappingModel>
            conventionUpdateByName,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
        IParameterSymbol? resultParameter,
        IParameterSymbol? contextParameter,
        LambdaExpressionSyntax transferScope,
        INamedTypeSymbol sourceMapper,
        ITypeSymbol? configuredDestination,
        IReadOnlyDictionary<ISymbol, string> localSubstitutions,
        IReadOnlyDictionary<ISymbol, ExpressionSyntax> localInitializers,
        CancellationToken cancellationToken,
        out ConventionMemberMappingPlan plan,
        out MappingFailureObservation? failure)
    {
        var create =
            ImmutableArray.CreateBuilder<TypeMapperMemberMappingModel>();
        var createPost =
            ImmutableArray.CreateBuilder<TypeMapperMemberMappingModel>();
        var mapReplacement =
            ImmutableArray.CreateBuilder<TypeMapperMemberMappingModel>();
        var mapReplacementPost =
            ImmutableArray.CreateBuilder<TypeMapperMemberMappingModel>();
        var update =
            ImmutableArray.CreateBuilder<TypeMapperMemberMappingModel>();
        var occupiedNames = new HashSet<string>(StringComparer.Ordinal);
        var observedRules =
            ImmutableArray.CreateBuilder<MemberRuleObservation>();
        var createNestedMapUsages =
            new DeclarativeNestedMapUsageRegistry(
                MappingExecutionPathSet.NoPrevious);
        var mapReplacementNestedMapUsages =
            new DeclarativeNestedMapUsageRegistry(
                MappingExecutionPathSet.UpdateWithPrevious);
        var updateNestedMapUsages =
            new DeclarativeNestedMapUsageRegistry(
                MappingExecutionPathSet.UpdateWithPrevious);

        foreach (var assignment in assignments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!occupiedNames.Add(assignment.MemberName))
            {
                plan = default;
                failure = BuildFailure(
                    mapping,
                    sourceMapper,
                    MappingFailureReason.MemberRuleInvalid,
                    UnsupportedMembersMessage,
                    transferScope,
                    assignment.Value,
                    createNestedMapUsages.Observations
                        .AddRange(
                            mapReplacementNestedMapUsages.Observations)
                        .AddRange(updateNestedMapUsages.Observations));
                return false;
            }

            if (TryGetHiddenImportedSlot(
                    configuredDestination,
                    destination,
                    assignment.MemberName,
                    cancellationToken,
                    out var importedMember,
                    out var hidingMember))
            {
                observedRules.Add(
                    new MemberRuleObservation(
                        importedMember,
                        SourceMember: null,
                        Origin: GetRuleOrigin(
                            assignment.Value,
                            semanticModel,
                            cancellationToken),
                        OriginNode: assignment.Value,
                        IsRequired: IsRequired(importedMember),
                        Lifecycle: GetLifecycle(
                            importedMember,
                            requiresAssignment: true),
                        HiddenImportedSlot: hidingMember,
                        InvalidReason:
                            MemberRuleInvalidReason.ImportedSlotHidden,
                        AssertedType: null,
                        DesignatorNode: assignment.DesignatorNode,
                        ResultDependencyOrigin: null,
                        SourceMapper: sourceMapper));
                continue;
            }

            if (!writableMembersByName.TryGetValue(
                    assignment.MemberName,
                    out var destinationMember))
            {
                plan = default;
                failure = BuildFailure(
                    mapping,
                    sourceMapper,
                    MappingFailureReason.MemberRuleInvalid,
                    UnsupportedMembersMessage,
                    transferScope,
                    assignment.Value,
                    createNestedMapUsages.Observations
                        .AddRange(
                            mapReplacementNestedMapUsages.Observations)
                        .AddRange(updateNestedMapUsages.Observations));
                return false;
            }

            var targetType = DeclarativeIntrinsic
                    .TryGetWrapperTargetType(
                        assignment.Value,
                        MetadataNames.Member,
                        semanticModel,
                        cancellationToken,
                        out var contextualTargetType)
                ? contextualTargetType
                : destinationMember.Type;

            if (DeclarativeMemberMarker.TryGetTypedMismatch(
                    assignment.Value,
                    destinationMember.Type,
                    semanticModel,
                    mapperType,
                    cancellationToken,
                    out var mismatchedMarker,
                    out var assertedType,
                    out var markerInvocation))
            {
                observedRules.Add(
                    BuildMemberRuleObservation(
                        convention,
                        destinationMember,
                        sourceMember: null,
                        ToMemberRuleOrigin(mismatchedMarker),
                        markerInvocation,
                        GetLifecycle(
                            destinationMember.Symbol,
                            requiresAssignment:
                                mismatchedMarker !=
                                DeclarativeIntrinsicKind.Ignore),
                        designatorNode: assignment.DesignatorNode,
                        invalidReason:
                            MemberRuleInvalidReason.MarkerTargetMismatch,
                        assertedType: assertedType,
                        resultDependencyOrigin: null,
                        sourceMapper: sourceMapper));
                continue;
            }

            if (DeclarativeMemberMarker.TryGetKind(
                    assignment.Value,
                    targetType,
                    semanticModel,
                    mapperType,
                    cancellationToken,
                    out var markerKind))
            {
                if (markerKind == DeclarativeMemberMarkerKind.Ignore)
                {
                    observedRules.Add(
                        BuildMemberRuleObservation(
                            convention,
                            destinationMember,
                            sourceMember: null,
                            MemberRuleOrigin.Ignore,
                            assignment.Value,
                            MemberLifecycleDependency.None,
                            assignment.DesignatorNode,
                            sourceMapper: sourceMapper));
                    continue;
                }

                if (!conventionCreateByName.TryGetValue(
                        destinationMember.Name,
                        out var automaticCreate))
                {
                    observedRules.Add(
                        BuildMemberRuleObservation(
                            convention,
                            destinationMember,
                            sourceMember: null,
                            MemberRuleOrigin.Auto,
                            assignment.Value,
                            GetLifecycle(
                                destinationMember.Symbol,
                                requiresAssignment: true),
                            assignment.DesignatorNode,
                            MemberRuleInvalidReason.AutoUnavailable,
                            sourceMapper: sourceMapper));
                    continue;
                }

                create.Add(automaticCreate);
                mapReplacement.Add(automaticCreate);

                if (conventionCreatePostByName.TryGetValue(
                        destinationMember.Name,
                        out var automaticCreatePost))
                {
                    createPost.Add(automaticCreatePost);
                    mapReplacementPost.Add(automaticCreatePost);
                }

                if (conventionUpdateByName.TryGetValue(
                        destinationMember.Name,
                        out var automaticUpdate))
                {
                    update.Add(automaticUpdate);
                }

                observedRules.Add(
                    BuildMemberRuleObservation(
                        convention,
                        destinationMember,
                        convention.Observation.Rules.FirstOrDefault(rule =>
                                StringComparer.Ordinal.Equals(
                                    rule.DestinationMember.Name,
                                    destinationMember.Name))
                            ?.SourceMember,
                        MemberRuleOrigin.Auto,
                        assignment.Value,
                        MemberLifecycleDependency.Creation |
                        (destinationMember.CanAssign
                            ? MemberLifecycleDependency
                                .ExistingDestination
                            : MemberLifecycleDependency.InitOnly),
                        assignment.DesignatorNode,
                        sourceMapper: sourceMapper));
                continue;
            }

            if (!TryBuildExplicitMapping(
                    assignment.Value,
                    assignment.DesignatorNode,
                    destinationMember,
                    mapping,
                    semanticModel,
                    mapperType,
                    sourceParameter,
                    previousParameter,
                    resultParameter,
                    contextParameter,
                    transferScope,
                    localSubstitutions,
                    localInitializers,
                    createNestedMapUsages,
                    mapReplacementNestedMapUsages,
                    updateNestedMapUsages,
                    targetType,
                    cancellationToken,
                    out var explicitPlan))
            {
                plan = default;
                failure = BuildFailure(
                    mapping,
                    sourceMapper,
                    MappingFailureReason.MemberRuleInvalid,
                    UnsupportedMembersMessage,
                    transferScope,
                    assignment.Value,
                    createNestedMapUsages.Observations
                        .AddRange(
                            mapReplacementNestedMapUsages.Observations)
                        .AddRange(updateNestedMapUsages.Observations));
                return false;
            }

            if (explicitPlan.Create is { } explicitCreate)
            {
                create.Add(explicitCreate);
            }

            if (explicitPlan.CreatePost is { } explicitCreatePost)
            {
                createPost.Add(explicitCreatePost);
            }

            if (explicitPlan.MapReplacement is
                    { } explicitReplacement)
            {
                mapReplacement.Add(explicitReplacement);
            }

            if (explicitPlan.MapReplacementPost is
                    { } replacementPost)
            {
                mapReplacementPost.Add(replacementPost);
            }

            if (explicitPlan.Update is { } existing)
            {
                update.Add(existing);
            }

            var lifecycle = MemberLifecycleDependency.Creation;

            if (!explicitPlan.IsCreationOnly)
            {
                lifecycle |=
                    MemberLifecycleDependency.ExistingDestination;
            }
            else
            {
                lifecycle |= MemberLifecycleDependency.InitOnly;
            }

            if (explicitPlan.IsResultDependent)
            {
                lifecycle |= MemberLifecycleDependency.Result;
            }

            observedRules.Add(
                BuildMemberRuleObservation(
                    convention,
                    destinationMember,
                    TryGetDirectSourceMember(
                        assignment.Value,
                        sourceParameter,
                        semanticModel,
                        cancellationToken),
                    MemberRuleOrigin.ExplicitValue,
                    assignment.Value,
                    lifecycle,
                    designatorNode: assignment.DesignatorNode,
                    resultDependencyOrigin:
                        resultParameter is null ||
                        !explicitPlan.IsResultDependent
                            ? null
                            : FindResultDependencyOrigin(
                                assignment.Value,
                                resultParameter,
                                semanticModel,
                                localInitializers,
                                cancellationToken),
                    sourceMapper: sourceMapper));
        }

        if (memberSelection == MemberSelectionValue.Auto)
        {
            create.AddRange(
                convention.Create.Where(candidate =>
                    !occupiedNames.Contains(
                        candidate.DestinationMemberName)));
            createPost.AddRange(
                convention.CreatePost.Where(candidate =>
                    !occupiedNames.Contains(
                        candidate.DestinationMemberName)));
            mapReplacement.AddRange(
                convention.MapReplacement.Where(candidate =>
                    !occupiedNames.Contains(
                        candidate.DestinationMemberName)));
            mapReplacementPost.AddRange(
                convention.MapReplacementPost.Where(candidate =>
                    !occupiedNames.Contains(
                        candidate.DestinationMemberName)));
            update.AddRange(
                convention.Update.Where(candidate =>
                    !occupiedNames.Contains(
                        candidate.DestinationMemberName)));

            observedRules.AddRange(
                convention.Observation.Rules.Where(rule =>
                    !occupiedNames.Contains(
                        rule.DestinationMember.Name)));
        }

        var immutableCreate = create.ToImmutable();
        var invalidMembers = observedRules
            .Where(static rule =>
                rule.InvalidReason != MemberRuleInvalidReason.None)
            .Select(static rule => rule.DestinationMember)
            .ToImmutableArray();

        plan = new ConventionMemberMappingPlan(
            immutableCreate,
            createPost.ToImmutable(),
            mapReplacement.ToImmutable(),
            mapReplacementPost.ToImmutable(),
            update.ToImmutable(),
            convention.Observation with
            {
                Rules = observedRules.ToImmutable(),
                RequiredObligations =
                    ConventionMemberMappingPlanner
                        .FindUnmappedRequiredMembers(
                            destination,
                            immutableCreate,
                            cancellationToken),
                NestedMappings = createNestedMapUsages.Observations
                    .AddRange(mapReplacementNestedMapUsages.Observations)
                    .AddRange(updateNestedMapUsages.Observations)
            },
            occupiedNames.ToImmutableArray());
        plan = plan with
        {
            Observation = plan.Observation with
            {
                RequiredObligations = plan.Observation.RequiredObligations
                    .Where(required => !invalidMembers.Any(invalid =>
                        SymbolEqualityComparer.Default.Equals(
                            required,
                            invalid)))
                    .ToImmutableArray()
            }
        };
        failure = null;
        return true;
    }

    private static ConventionMemberMappingPlan MergePlans(
        IEnumerable<ConventionMemberMappingPlan> plans,
        MemberSelectionValue memberSelection,
        ConventionMemberMappingPlan convention,
        ITypeSymbol destination,
        MappingPairCapabilities capabilities,
        CSharpCompilation compilation,
        CancellationToken cancellationToken)
    {
        var immutablePlans = plans.ToImmutableArray();
        var occupiedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var plan in immutablePlans)
        {
            occupiedNames.UnionWith(
                plan.ConfiguredMemberNames.IsDefault
                    ? []
                    : plan.ConfiguredMemberNames);
        }

        ImmutableArray<TypeMapperMemberMappingModel> Merge(
            Func<
                ConventionMemberMappingPlan,
                ImmutableArray<TypeMapperMemberMappingModel>> selector,
            ImmutableArray<TypeMapperMemberMappingModel> conventions)
        {
            var result = new List<TypeMapperMemberMappingModel>();

            foreach (var plan in immutablePlans)
            {
                var overriddenNames = plan.ConfiguredMemberNames.IsDefault
                    ? []
                    : plan.ConfiguredMemberNames;

                result.RemoveAll(mapping =>
                    overriddenNames.Contains(
                        mapping.DestinationMemberName,
                        StringComparer.Ordinal));
                result.AddRange(selector(plan));
            }

            if (memberSelection == MemberSelectionValue.Auto)
            {
                result.AddRange(conventions.Where(mapping =>
                    !occupiedNames.Contains(
                        mapping.DestinationMemberName)));
            }

            return result.ToImmutableArray();
        }

        var create = Merge(
            static plan => plan.Create,
            convention.Create);
        var createPost = Merge(
            static plan => plan.CreatePost,
            convention.CreatePost);
        var mapReplacement = Merge(
            static plan => plan.MapReplacement,
            convention.MapReplacement);
        var mapReplacementPost = Merge(
            static plan => plan.MapReplacementPost,
            convention.MapReplacementPost);
        var update = Merge(
            static plan => plan.Update,
            convention.Update);
        var rules = memberSelection == MemberSelectionValue.Auto
            ? convention.Observation.Rules.ToList()
            : new List<MemberRuleObservation>();

        foreach (var plan in immutablePlans)
        {
            var overriddenNames = plan.ConfiguredMemberNames.IsDefault
                ? []
                : plan.ConfiguredMemberNames;

            rules.RemoveAll(rule =>
                overriddenNames.Contains(
                    rule.DestinationMember.Name,
                    StringComparer.Ordinal));
            rules.AddRange(plan.Observation.Rules.Where(rule =>
                overriddenNames.Contains(
                    rule.DestinationMember.Name,
                    StringComparer.Ordinal)));
        }

        var nestedMappings = new List<NestedMappingObservation>();

        foreach (var plan in immutablePlans)
        {
            var overriddenNames = plan.ConfiguredMemberNames.IsDefault
                ? []
                : plan.ConfiguredMemberNames;

            nestedMappings.RemoveAll(observation =>
                observation.TargetName is { } targetName &&
                overriddenNames.Contains(
                    targetName,
                    StringComparer.Ordinal));
            nestedMappings.AddRange(
                (plan.Observation.NestedMappings.IsDefault
                    ? []
                    : plan.Observation.NestedMappings)
                .Where(observation =>
                    observation.TargetName is null ||
                    overriddenNames.Contains(
                        observation.TargetName,
                        StringComparer.Ordinal)));
        }

        return new ConventionMemberMappingPlan(
            create,
            createPost,
            mapReplacement,
            mapReplacementPost,
            update,
            convention.Observation with
            {
                Rules = rules.ToImmutableArray(),
                RequiredObligations =
                    ConventionMemberMappingPlanner
                        .FindUnmappedRequiredMembers(
                            destination,
                            create,
                            cancellationToken)
                        .Where(required => !rules.Any(rule =>
                            rule.InvalidReason !=
                                MemberRuleInvalidReason.None &&
                            SymbolEqualityComparer.Default.Equals(
                                rule.DestinationMember,
                                required)))
                        .ToImmutableArray(),
                Terminals = immutablePlans.SelectMany(plan =>
                        plan.Observation.Terminals.IsDefault
                            ? []
                            : plan.Observation.Terminals)
                    .ToImmutableArray(),
                NestedMappings = nestedMappings.ToImmutableArray(),
                SourceDiscards = immutablePlans.SelectMany(plan =>
                        plan.Observation.SourceDiscards.IsDefault
                            ? []
                            : plan.Observation.SourceDiscards)
                    .ToImmutableArray(),
                PlanOrigin = immutablePlans
                    .Select(static plan => plan.Observation.PlanOrigin)
                    .LastOrDefault(static origin => origin is not null)
            },
            occupiedNames.ToImmutableArray(),
            immutablePlans.Select(static plan => plan.Failure)
                .FirstOrDefault(static failure => failure is not null));
    }

    private static bool TryBuildExplicitMapping(
        ExpressionSyntax expression,
        SyntaxNode? targetDesignator,
        ConventionWritableMember destinationMember,
        TypeMapperMappingModel mapping,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
        IParameterSymbol? resultParameter,
        IParameterSymbol? contextParameter,
        LambdaExpressionSyntax transferScope,
        IReadOnlyDictionary<ISymbol, string> localSubstitutions,
        IReadOnlyDictionary<ISymbol, ExpressionSyntax> localInitializers,
        DeclarativeNestedMapUsageRegistry createNestedMapUsages,
        DeclarativeNestedMapUsageRegistry mapReplacementNestedMapUsages,
        DeclarativeNestedMapUsageRegistry updateNestedMapUsages,
        ITypeSymbol targetType,
        CancellationToken cancellationToken,
        out ExplicitMemberMappingPlan plan)
    {
        var createSucceeded = DeclarativeDependencyExpressionBuilder
            .TryRewriteWithContext(
                expression,
                semanticModel,
                mapperType,
                sourceParameter,
                mapping.NonNullSourceName,
                previousParameter,
                BuildPreviousSubstitution(mapping, hasPrevious: false),
                resultParameter,
                mapping.ResultLocalName,
                contextParameter,
                contextName: "context",
                transferScope,
                localSubstitutions,
                targetType,
                new DeclarativeNestedMapTargetContext(
                    targetType,
                    destinationMember.Name,
                    DeclarativeNestedMapOperation.Create,
                    CurrentDestinationExpression: null,
                    CurrentDestinationType: null,
                    destinationMember.Symbol,
                    targetDesignator,
                    CurrentDestinationSymbol: null,
                    MappingExecutionPathSet.NoPrevious),
                createNestedMapUsages,
                cancellationToken,
                out var createExpression,
                out var createDependency);
        var mapReplacementSucceeded = DeclarativeDependencyExpressionBuilder
            .TryRewriteWithContext(
                expression,
                semanticModel,
                mapperType,
                sourceParameter,
                mapping.NonNullSourceName,
                previousParameter,
                BuildPreviousSubstitution(mapping, hasPrevious: true),
                resultParameter,
                mapping.ResultLocalName,
                contextParameter,
                contextName: "context",
                transferScope,
                localSubstitutions,
                targetType,
                new DeclarativeNestedMapTargetContext(
                    targetType,
                    destinationMember.Name,
                    DeclarativeNestedMapOperation.Update,
                    mapping.ResultLocalName + "." +
                    Identifier(destinationMember.Name),
                    destinationMember.Type,
                    destinationMember.Symbol,
                    targetDesignator,
                    destinationMember.Symbol,
                    MappingExecutionPathSet.UpdateWithPrevious),
                mapReplacementNestedMapUsages,
                cancellationToken,
                out var mapReplacementExpression,
                out var mapReplacementDependency);
        var updateSucceeded = DeclarativeDependencyExpressionBuilder
            .TryRewriteWithContext(
                expression,
                semanticModel,
                mapperType,
                sourceParameter,
                mapping.NonNullSourceName,
                previousParameter,
                BuildPreviousSubstitution(mapping, hasPrevious: true),
                resultParameter,
                "destination",
                contextParameter,
                contextName: "context",
                transferScope,
                localSubstitutions,
                targetType,
                new DeclarativeNestedMapTargetContext(
                    targetType,
                    destinationMember.Name,
                    DeclarativeNestedMapOperation.Update,
                    "destination." +
                    Identifier(destinationMember.Name),
                    destinationMember.Type,
                    destinationMember.Symbol,
                    targetDesignator,
                    destinationMember.Symbol,
                    MappingExecutionPathSet.UpdateWithPrevious),
                updateNestedMapUsages,
                cancellationToken,
                out var updateExpression,
                out var updateDependency);

        if (!createSucceeded &&
            !HasNestedFailure(createNestedMapUsages) ||
            !mapReplacementSucceeded &&
            !HasNestedFailure(mapReplacementNestedMapUsages) ||
            !updateSucceeded &&
            !HasNestedFailure(updateNestedMapUsages))
        {
            plan = default;
            return false;
        }

        var isResultDependent = resultParameter is not null &&
            ReferencesParameterAtRuntime(
                expression,
                resultParameter,
                semanticModel,
                localInitializers,
                new HashSet<ISymbol>(
                    SymbolEqualityComparer.Default),
                cancellationToken);
        var valueTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                targetType);
        TypeMapperMemberMappingModel BuildMapping(
            string valueExpression,
            TypeMapperDependencyExpressionModel? dependencyExpression,
            bool resultDependent) =>
            new(
                SourceMemberName: string.Empty,
                destinationMember.Name,
                destinationMember.IsRequired,
                SourceValueLocalName: null,
                ExplicitValueExpression: valueExpression,
                ExplicitValueTypeName: valueTypeName,
                IsResultDependent: resultDependent,
                DependencyExpression: dependencyExpression);

        var create = createSucceeded
            ? BuildMapping(
                createExpression,
                createDependency,
                isResultDependent)
            : (TypeMapperMemberMappingModel?)null;
        var replacementIsResultDependent =
            isResultDependent ||
            mapReplacementSucceeded && ReferencesIdentifier(
                mapReplacementExpression,
                mapping.ResultLocalName);
        var mapReplacement = mapReplacementSucceeded
            ? BuildMapping(
                mapReplacementExpression,
                mapReplacementDependency,
                replacementIsResultDependent)
            : (TypeMapperMemberMappingModel?)null;
        var update = updateSucceeded
            ? BuildMapping(
                updateExpression,
                updateDependency,
                isResultDependent)
            : (TypeMapperMemberMappingModel?)null;

        plan = new ExplicitMemberMappingPlan(
            Create: isResultDependent ? null : create,
            CreatePost: destinationMember.CanAssign && createSucceeded
                ? create
                : null,
            MapReplacement: replacementIsResultDependent
                ? null
                : mapReplacement,
            MapReplacementPost:
                destinationMember.CanAssign && mapReplacementSucceeded
                ? mapReplacement
                : null,
            Update: destinationMember.CanAssign && updateSucceeded
                ? update
                : null,
            IsCreationOnly: !destinationMember.CanAssign,
            IsResultDependent: isResultDependent);
        return true;
    }

    private static bool HasNestedFailure(
        DeclarativeNestedMapUsageRegistry registry)
    {
        return registry.Observations.Any(static observation =>
            observation.FailureKind != NestedMappingFailureKind.None);
    }

    private static bool ReferencesParameterAtRuntime(
        ExpressionSyntax expression,
        IParameterSymbol parameter,
        SemanticModel semanticModel,
        IReadOnlyDictionary<ISymbol, ExpressionSyntax> localInitializers,
        HashSet<ISymbol> visitedLocals,
        CancellationToken cancellationToken)
    {
        foreach (var identifier in expression
                     .DescendantNodesAndSelf(
                         node => !IsConstantNameOf(node, semanticModel))
                     .OfType<IdentifierNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(
                            identifier,
                            cancellationToken)
                        .Symbol,
                    parameter))
            {
                return true;
            }

            var symbol = semanticModel.GetSymbolInfo(
                    identifier,
                    cancellationToken)
                .Symbol;

            if (symbol is not null &&
                visitedLocals.Add(symbol) &&
                localInitializers.TryGetValue(
                    symbol,
                    out var initializer) &&
                ReferencesParameterAtRuntime(
                    initializer,
                    parameter,
                    semanticModel,
                    localInitializers,
                    visitedLocals,
                    cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<DeclarativeLeafSyntaxNode>
        EnumerateLeaves(DeclarativeControlFlowSyntaxNode node)
    {
        switch (node)
        {
            case DeclarativeLeafSyntaxNode leaf:
                yield return leaf;
                yield break;

            case DeclarativeLocalDeclarationsSyntaxNode locals:
                foreach (var leaf in EnumerateLeaves(locals.Next))
                {
                    yield return leaf;
                }

                yield break;

            case DeclarativeEvaluationSyntaxNode evaluation:
                foreach (var leaf in EnumerateLeaves(evaluation.Next))
                {
                    yield return leaf;
                }

                yield break;

            case DeclarativeConditionalSyntaxNode conditional:
                foreach (var leaf in EnumerateLeaves(
                             conditional.WhenTrue))
                {
                    yield return leaf;
                }

                foreach (var leaf in EnumerateLeaves(
                             conditional.WhenFalse))
                {
                    yield return leaf;
                }

                yield break;

            case DeclarativeSwitchSyntaxNode switchNode:
                foreach (var section in switchNode.Sections)
                {
                    foreach (var leaf in EnumerateLeaves(
                                 section.Branch))
                    {
                        yield return leaf;
                    }
                }

                if (switchNode.Continuation is { } continuation)
                {
                    foreach (var leaf in EnumerateLeaves(continuation))
                    {
                        yield return leaf;
                    }
                }

                yield break;
        }
    }

    private static bool IsConstantNameOf(
        SyntaxNode node,
        SemanticModel semanticModel)
    {
        return node is InvocationExpressionSyntax
               {
                   Expression: IdentifierNameSyntax
                   {
                       Identifier.ValueText: "nameof"
                   }
               } invocation &&
               semanticModel.GetConstantValue(invocation).HasValue;
    }

    private static bool ReferencesIdentifier(
        string expression,
        string identifier)
    {
        return SyntaxFactory.ParseExpression(expression)
            .DescendantTokens()
            .Any(token =>
                token.IsKind(SyntaxKind.IdentifierToken) &&
                StringComparer.Ordinal.Equals(
                    token.ValueText,
                    identifier));
    }

    private static string Identifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value
            : value;
    }

    private static PreviousExpressionSubstitution
        BuildPreviousSubstitution(
            TypeMapperMappingModel mapping,
            bool hasPrevious)
    {
        var optionTypeName =
            "global::Morphant.Option<" +
            mapping.NonNullDestinationTypeName +
            ">";
        var optionExpression = hasPrevious
            ? optionTypeName + ".Some(destination)"
            : optionTypeName + ".None";

        return hasPrevious
            ? new PreviousExpressionSubstitution(
                optionExpression,
                "destination",
                "true")
            : new PreviousExpressionSubstitution(
                optionExpression,
                optionExpression + ".Value",
                "false");
    }

    private static bool TryGetLambdaParameters(
        LambdaExpressionSyntax lambda,
        SemanticModel semanticModel,
        MembersConfigurationForm form,
        CancellationToken cancellationToken,
        out IParameterSymbol sourceParameter,
        out IParameterSymbol? previousParameter,
        out IParameterSymbol? resultParameter,
        out IParameterSymbol? contextParameter)
    {
        var parameters = lambda switch
        {
            SimpleLambdaExpressionSyntax simple =>
                new[] { simple.Parameter },
            ParenthesizedLambdaExpressionSyntax parenthesized =>
                parenthesized.ParameterList.Parameters.ToArray(),
            _ => []
        };
        var hasPrevious = form is not MembersConfigurationForm.Source;
        var hasResult = form is
            MembersConfigurationForm.SourcePreviousAndResult or
            MembersConfigurationForm.SourcePreviousResultAndContext;
        var hasContext = form is
            MembersConfigurationForm.SourcePreviousResultAndContext;
        var expectedCount = 1 +
            (hasPrevious ? 1 : 0) +
            (hasResult ? 1 : 0) +
            (hasContext ? 1 : 0);

        if (parameters.Length != expectedCount ||
            semanticModel.GetDeclaredSymbol(
                    parameters[0],
                    cancellationToken) is not IParameterSymbol resolvedSource)
        {
            sourceParameter = null!;
            previousParameter = null;
            resultParameter = null;
            contextParameter = null;
            return false;
        }

        sourceParameter = resolvedSource;
        var index = 1;
        previousParameter = hasPrevious
            ? semanticModel.GetDeclaredSymbol(
                    parameters[index++],
                    cancellationToken) as IParameterSymbol
            : null;
        resultParameter = hasResult
            ? semanticModel.GetDeclaredSymbol(
                    parameters[index++],
                    cancellationToken) as IParameterSymbol
            : null;
        contextParameter = hasContext
            ? semanticModel.GetDeclaredSymbol(
                    parameters[index],
                    cancellationToken) as IParameterSymbol
            : null;
        return (!hasPrevious || previousParameter is not null) &&
               (!hasResult || resultParameter is not null) &&
               (!hasContext || contextParameter is not null);
    }

    private static bool TryGetAssignments(
        LambdaExpressionSyntax lambda,
        out ImmutableArray<AssignmentExpressionSyntax> assignments)
    {
        var expression = lambda.ExpressionBody;

        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        var initializer = expression switch
        {
            ImplicitObjectCreationExpressionSyntax implicitCreation =>
                implicitCreation.Initializer,
            ObjectCreationExpressionSyntax objectCreation =>
                objectCreation.Initializer,
            _ => null
        };

        if (initializer is null ||
            !initializer.IsKind(SyntaxKind.ObjectInitializerExpression))
        {
            assignments = default;
            return false;
        }

        var result =
            ImmutableArray.CreateBuilder<AssignmentExpressionSyntax>(
                initializer.Expressions.Count);

        foreach (var initializerExpression in initializer.Expressions)
        {
            if (initializerExpression is not AssignmentExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.SimpleAssignmentExpression
                } assignment)
            {
                assignments = default;
                return false;
            }

            result.Add(assignment);
        }

        assignments = result.ToImmutable();
        return true;
    }

    private static ITypeSymbol? TryGetConfiguredDestinationType(
        MembersConfigurationModel configuration,
        CancellationToken cancellationToken)
    {
        if (configuration.Invocation.Expression is not
                MemberAccessExpressionSyntax memberAccess ||
            configuration.Expression.SemanticModel.GetTypeInfo(
                    memberAccess.Expression,
                    cancellationToken)
                .Type is not INamedTypeSymbol
                {
                    TypeArguments.Length: 2
                } builderType ||
            !StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    builderType.OriginalDefinition),
                "Morphant.MapperBuilder`2"))
        {
            return null;
        }

        return MappingTypeNormalization.NormalizePreviousDestination(
            builderType.TypeArguments[1],
            configuration.Expression.SemanticModel.Compilation);
    }

    private static bool TryGetHiddenImportedSlot(
        ITypeSymbol? configuredDestination,
        ITypeSymbol currentDestination,
        string memberName,
        CancellationToken cancellationToken,
        out ISymbol importedMember,
        out ISymbol hidingMember)
    {
        if (configuredDestination is null ||
            StringComparer.Ordinal.Equals(
                MappingTypeIdentityPolicy.Create(configuredDestination).Key,
                MappingTypeIdentityPolicy.Create(currentDestination).Key) ||
            ConventionMemberMappingPlanner.FindEffectiveInstanceMember(
                configuredDestination,
                memberName,
                cancellationToken) is not { } imported ||
            imported is not (IPropertySymbol or IFieldSymbol) ||
            ConventionMemberMappingPlanner.FindEffectiveInstanceMember(
                currentDestination,
                memberName,
                cancellationToken) is not { } current ||
            IsSameMemberIdentity(imported, current))
        {
            importedMember = null!;
            hidingMember = null!;
            return false;
        }

        importedMember = imported;
        hidingMember = current;
        return true;
    }

    private static bool IsSameMemberIdentity(
        ISymbol imported,
        ISymbol current)
    {
        if (SymbolEqualityComparer.Default.Equals(imported, current) ||
            SymbolEqualityComparer.Default.Equals(
                imported.OriginalDefinition,
                current.OriginalDefinition) ||
            StringComparer.Ordinal.Equals(
                imported.GetDocumentationCommentId(),
                current.GetDocumentationCommentId()))
        {
            return true;
        }

        if (IsSameDeclaredSlot(imported, current))
        {
            return true;
        }

        if (imported.ContainingType?.TypeKind == TypeKind.Interface &&
            current.ContainingType is { } currentType &&
            FindInterfaceImplementation(
                currentType,
                imported) is
                { } implementation &&
            IsSameDeclaredSlot(implementation, current))
        {
            return true;
        }

        if (current is not IPropertySymbol property)
        {
            return false;
        }

        for (var overridden = property.OverriddenProperty;
             overridden is not null;
             overridden = overridden.OverriddenProperty)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    imported.OriginalDefinition,
                    overridden.OriginalDefinition) ||
                StringComparer.Ordinal.Equals(
                    imported.GetDocumentationCommentId(),
                    overridden.GetDocumentationCommentId()))
            {
                return true;
            }
        }

        return false;
    }

    private static ISymbol? FindInterfaceImplementation(
        INamedTypeSymbol currentType,
        ISymbol imported)
    {
        if (currentType.FindImplementationForInterfaceMember(imported) is
            { } direct)
        {
            return direct;
        }

        if (imported.ContainingType is not { } importedInterface)
        {
            return null;
        }

        var importedKey = MappingTypeIdentityPolicy.Create(
                importedInterface)
            .Key;
        var equivalentInterface = currentType.AllInterfaces.FirstOrDefault(
            candidate => StringComparer.Ordinal.Equals(
                MappingTypeIdentityPolicy.Create(candidate).Key,
                importedKey));
        var equivalentMember = equivalentInterface?.GetMembers(imported.Name)
            .FirstOrDefault(candidate => candidate.Kind == imported.Kind);

        return equivalentMember is null
            ? null
            : currentType.FindImplementationForInterfaceMember(
                equivalentMember);
    }

    private static bool IsSameDeclaredSlot(
        ISymbol left,
        ISymbol right)
    {
        if (left.Kind != right.Kind ||
            !StringComparer.Ordinal.Equals(
                left.MetadataName,
                right.MetadataName) ||
            left.ContainingType is not { } leftType ||
            right.ContainingType is not { } rightType)
        {
            return false;
        }

        return StringComparer.Ordinal.Equals(
                   leftType.ContainingAssembly.Identity.ToString(),
                   rightType.ContainingAssembly.Identity.ToString()) &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       leftType.OriginalDefinition),
                   SymbolNameHelper.GetFullMetadataName(
                       rightType.OriginalDefinition));
    }

    private static bool IsRequired(ISymbol member) =>
        member is IPropertySymbol { IsRequired: true } or
            IFieldSymbol { IsRequired: true };

    private static MemberLifecycleDependency GetLifecycle(
        ISymbol member,
        bool requiresAssignment)
    {
        if (!requiresAssignment)
        {
            return MemberLifecycleDependency.Creation |
                   MemberLifecycleDependency.ExistingDestination;
        }

        var canAssign = member switch
        {
            IPropertySymbol
            {
                SetMethod: { IsInitOnly: false }
            } => true,
            IFieldSymbol { IsReadOnly: false } => true,
            _ => false
        };

        return MemberLifecycleDependency.Creation |
               (canAssign
                   ? MemberLifecycleDependency.ExistingDestination
                   : MemberLifecycleDependency.InitOnly);
    }

    private static MemberRuleOrigin GetRuleOrigin(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var invocation in expression.DescendantNodesAndSelf()
                     .OfType<InvocationExpressionSyntax>())
        {
            if (!DeclarativeIntrinsic.TryGetKind(
                    invocation,
                    semanticModel,
                    cancellationToken,
                    out var kind,
                    out _))
            {
                continue;
            }

            return ToMemberRuleOrigin(kind);
        }

        return MemberRuleOrigin.ExplicitValue;
    }

    private static MemberRuleOrigin ToMemberRuleOrigin(
        DeclarativeIntrinsicKind kind) => kind switch
    {
        DeclarativeIntrinsicKind.Auto => MemberRuleOrigin.Auto,
        DeclarativeIntrinsicKind.Ignore => MemberRuleOrigin.Ignore,
        _ => MemberRuleOrigin.ExplicitValue
    };

    private static SyntaxNode? FindResultDependencyOrigin(
        ExpressionSyntax expression,
        IParameterSymbol resultParameter,
        SemanticModel semanticModel,
        IReadOnlyDictionary<ISymbol, ExpressionSyntax> localInitializers,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);

        SyntaxNode? Find(ExpressionSyntax candidate)
        {
            foreach (var identifier in candidate.DescendantNodesAndSelf()
                         .OfType<IdentifierNameSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var symbol = semanticModel.GetSymbolInfo(
                        identifier,
                        cancellationToken)
                    .Symbol;

                if (SymbolEqualityComparer.Default.Equals(
                        symbol,
                        resultParameter))
                {
                    return identifier;
                }

                if (symbol is not null &&
                    visited.Add(symbol) &&
                    localInitializers.TryGetValue(
                        symbol,
                        out var initializer) &&
                    Find(initializer) is { } dependency)
                {
                    return dependency;
                }
            }

            return null;
        }

        return Find(expression);
    }

    private static SyntaxNode? FindResultDependentPathOrigin(
        DeclarativeControlFlowSyntaxNode root,
        DeclarativeLeafSyntaxNode target,
        IParameterSymbol resultParameter,
        SemanticModel semanticModel,
        IReadOnlyDictionary<ISymbol, ExpressionSyntax> localInitializers,
        CancellationToken cancellationToken)
    {
        bool TryFind(
            DeclarativeControlFlowSyntaxNode node,
            SyntaxNode? inheritedDependency,
            out SyntaxNode? dependency)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (node)
            {
                case DeclarativeLeafSyntaxNode leaf:
                    dependency = inheritedDependency;
                    return ReferenceEquals(leaf, target);

                case DeclarativeLocalDeclarationsSyntaxNode locals:
                    return TryFind(
                        locals.Next,
                        inheritedDependency,
                        out dependency);

                case DeclarativeEvaluationSyntaxNode evaluation:
                    return TryFind(
                        evaluation.Next,
                        inheritedDependency,
                        out dependency);

                case DeclarativeConditionalSyntaxNode conditional:
                {
                    var conditionDependency = inheritedDependency ??
                        FindResultDependencyOrigin(
                            conditional.Condition,
                            resultParameter,
                            semanticModel,
                            localInitializers,
                            cancellationToken);

                    return TryFind(
                               conditional.WhenTrue,
                               conditionDependency,
                               out dependency) ||
                           TryFind(
                               conditional.WhenFalse,
                               conditionDependency,
                               out dependency);
                }

                case DeclarativeSwitchSyntaxNode switchNode:
                {
                    var switchDependency = inheritedDependency ??
                        FindResultDependencyOrigin(
                            switchNode.GoverningExpression,
                            resultParameter,
                            semanticModel,
                            localInitializers,
                            cancellationToken);

                    foreach (var section in switchNode.Sections)
                    {
                        var sectionDependency = switchDependency;

                        if (sectionDependency is null)
                        {
                            foreach (var label in section.Labels)
                            {
                                var labelExpression =
                                    label.WhenCondition ?? label.Value;

                                if (labelExpression is not null &&
                                    FindResultDependencyOrigin(
                                        labelExpression,
                                        resultParameter,
                                        semanticModel,
                                        localInitializers,
                                        cancellationToken) is
                                        { } labelDependency)
                                {
                                    sectionDependency = labelDependency;
                                    break;
                                }
                            }
                        }

                        if (TryFind(
                                section.Branch,
                                sectionDependency,
                                out dependency))
                        {
                            return true;
                        }
                    }

                    if (switchNode.Continuation is { } continuation)
                    {
                        return TryFind(
                            continuation,
                            switchDependency,
                            out dependency);
                    }

                    dependency = null;
                    return false;
                }

                default:
                    dependency = null;
                    return false;
            }
        }

        return TryFind(root, inheritedDependency: null, out var dependency)
            ? dependency
            : null;
    }

    private static bool TryGetOmittedProducer(
        ExpressionSyntax expression,
        out ExpressionSyntax producer)
    {
        expression = DeclarativeIntrinsic.UnwrapTransparentSyntax(expression);

        if (expression is CastExpressionSyntax cast &&
            TryGetOmittedProducer(cast.Expression, out producer))
        {
            return true;
        }

        if (expression is LiteralExpressionSyntax
        {
            RawKind: (int)SyntaxKind.NullLiteralExpression or
                (int)SyntaxKind.DefaultLiteralExpression
        } or DefaultExpressionSyntax)
        {
            producer = expression;
            return true;
        }

        producer = null!;
        return false;
    }

    private static MappingFailureObservation BuildFailure(
        TypeMapperMappingModel mapping,
        MembersConfigurationModel configuration,
        MappingFailureReason reason,
        string recoveryMessage,
        SyntaxNode? offendingNode = null)
    {
        return BuildFailure(
            mapping,
            configuration.Expression.DeclaringMapperType,
            reason,
            recoveryMessage,
            configuration.Invocation,
            offendingNode);
    }

    private static MemberRuleObservation BuildMemberRuleObservation(
        ConventionMemberMappingPlan convention,
        ConventionWritableMember destinationMember,
        ISymbol? sourceMember,
        MemberRuleOrigin origin,
        SyntaxNode originNode,
        MemberLifecycleDependency lifecycle,
        SyntaxNode? designatorNode = null,
        MemberRuleInvalidReason invalidReason =
            MemberRuleInvalidReason.None,
        ITypeSymbol? assertedType = null,
        SyntaxNode? resultDependencyOrigin = null,
        INamedTypeSymbol? sourceMapper = null)
    {
        return new MemberRuleObservation(
            destinationMember.Symbol,
            sourceMember,
            origin,
            originNode,
            destinationMember.IsRequired,
            lifecycle,
            HiddenImportedSlot: null,
            invalidReason,
            assertedType,
            designatorNode,
            resultDependencyOrigin,
            sourceMapper,
            destinationMember.Type);
    }

    private static ISymbol? TryGetDirectSourceMember(
        ExpressionSyntax expression,
        IParameterSymbol sourceParameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        if (expression is not MemberAccessExpressionSyntax memberAccess ||
            !SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(
                        memberAccess.Expression,
                        cancellationToken)
                    .Symbol,
                sourceParameter))
        {
            return null;
        }

        return semanticModel.GetSymbolInfo(
                memberAccess,
                cancellationToken)
            .Symbol;
    }

    private static MappingFailureObservation BuildFailure(
        TypeMapperMappingModel mapping,
        INamedTypeSymbol sourceMapper,
        MappingFailureReason reason,
        string recoveryMessage,
        SyntaxNode originNode,
        SyntaxNode? offendingNode = null,
        ImmutableArray<NestedMappingObservation> nestedObservations =
            default)
    {
        var nestedObservation = nestedObservations.IsDefaultOrEmpty
            ? null
            : nestedObservations.FirstOrDefault(static observation =>
                observation.FailureKind != NestedMappingFailureKind.None);

        if (nestedObservation is not null)
        {
            reason = ClassifyNestedFailure(
                nestedObservation,
                reason);
        }

        return MappingFailureObservation.Create(
            mapping.AnalysisContext,
            reason,
            recoveryMessage,
            nestedObservation is null
                ? MappingObservationOriginKind.Callback
                : MappingObservationOriginKind.NestedMarker,
            MappingAffectedPath.All(
                nestedObservation is null
                    ? MappingPlanPhase.Members
                    : MappingPlanPhase.NestedMapping) with
            {
                BranchOrigin = offendingNode
            },
            originNode,
            sourceMapper,
            nestedObservation?.Producer ?? offendingNode,
            nestedObservation?.ProducerSymbol,
            nestedObservations: nestedObservations);
    }

    private static MappingFailureReason ClassifyNestedFailure(
        NestedMappingObservation observation,
        MappingFailureReason fallback)
    {
        return observation.FailureKind switch
        {
            NestedMappingFailureKind.SourceTypeUnknown or
            NestedMappingFailureKind.ParameterlessSourceUnavailable or
            NestedMappingFailureKind.DestinationTypeUnknown =>
                MappingFailureReason.NestedPairUnknown,
            NestedMappingFailureKind.ResultIncompatible =>
                MappingFailureReason.NestedResultIncompatible,
            NestedMappingFailureKind.ExplicitDestinationIncompatible or
            NestedMappingFailureKind.ExplicitNullForNonNullableValue or
            NestedMappingFailureKind.AdaptiveCurrentUnavailable or
            NestedMappingFailureKind.AdaptiveCurrentIncompatible or
            NestedMappingFailureKind.AdaptiveCurrentAmbiguous or
            NestedMappingFailureKind.ReadOnlyProxyInvalid =>
                MappingFailureReason.NestedUpdateDestinationInvalid,
            _ => fallback
        };
    }
}

internal readonly record struct BasicMembersMappingResult(
    ConventionMemberMappingPlan Plan,
    MembersDeclarativeControlFlowPlan? ControlFlow,
    MappingFailureObservation? Failure)
{
    public static BasicMembersMappingResult Unsupported(
        MappingFailureObservation failure) =>
        new(default, ControlFlow: null, failure);
}

internal sealed record MembersDeclarativeControlFlowPlan(
    DeclarativeControlFlowProgram Program,
    IReadOnlyDictionary<
        DeclarativeLeafSyntaxNode,
        ConventionMemberMappingPlan> Leaves,
    SemanticModel SemanticModel,
    INamedTypeSymbol MapperType,
    IParameterSymbol SourceParameter,
    IParameterSymbol? PreviousParameter,
    IParameterSymbol? ResultParameter,
    IParameterSymbol? ContextParameter,
    LambdaExpressionSyntax TransferScope,
    IReadOnlyDictionary<ISymbol, ExpressionSyntax> LocalInitializers);

internal readonly record struct ExplicitMemberMappingPlan(
    TypeMapperMemberMappingModel? Create,
    TypeMapperMemberMappingModel? CreatePost,
    TypeMapperMemberMappingModel? MapReplacement,
    TypeMapperMemberMappingModel? MapReplacementPost,
    TypeMapperMemberMappingModel? Update,
    bool IsCreationOnly,
    bool IsResultDependent);
