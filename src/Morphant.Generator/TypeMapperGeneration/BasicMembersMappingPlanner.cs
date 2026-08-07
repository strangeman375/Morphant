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

            if (result.UnsupportedMessage is not null ||
                result.ControlFlow is not null)
            {
                return result.UnsupportedMessage is { } message
                    ? BasicMembersMappingResult.Unsupported(message)
                    : BasicMembersMappingResult.Unsupported(
                        UnsupportedMembersMessage);
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
            UnsupportedMessage: null);
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
                    UnsupportedMessage: null);
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
                    ConventionMemberMappingPlanner
                        .HasUnmappedRequiredMembers(
                            destination,
                            emptyCreate,
                            cancellationToken),
                    HasExplicitCreationOnlyMappings: false,
                    HasResultDependentCreationOnlyMappings: false),
                ControlFlow: null,
                UnsupportedMessage: null);
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
                out var resultParameter))
        {
            return BasicMembersMappingResult.Unsupported(
                UnsupportedMembersMessage);
        }

        var controlFlowResult = DeclarativeControlFlowPlanner.Build(
            lambda,
            configured.Expression.SemanticModel,
            cancellationToken);

        if (controlFlowResult is UnsupportedDeclarativeControlFlow
            unsupportedControlFlow)
        {
            return BasicMembersMappingResult.Unsupported(
                unsupportedControlFlow.Message);
        }

        if (controlFlowResult is not DeclarativeControlFlowProgram
            controlFlow)
        {
            return BasicMembersMappingResult.Unsupported(
                UnsupportedMembersMessage);
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
        var leaves =
            new Dictionary<
                DeclarativeLeafSyntaxNode,
                ConventionMemberMappingPlan>();
        string? leafUnsupportedMessage = null;

        bool BuildLeaf(DeclarativeLeafSyntaxNode leaf)
        {
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
                    lambda,
                    controlFlow.RuntimeLocalPlaceholders,
                    runtimeLocalInitializers,
                    cancellationToken,
                    out var plan,
                    out leafUnsupportedMessage))
            {
                return false;
            }

            leaves.Add(leaf, plan);
            return true;
        }

        foreach (var leaf in EnumerateLeaves(controlFlow.Root))
        {
            if (!BuildLeaf(leaf))
            {
                return BasicMembersMappingResult.Unsupported(
                    leafUnsupportedMessage ??
                    UnsupportedMembersMessage);
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
                    lambda,
                    runtimeLocalInitializers)
                : null,
            UnsupportedMessage: null);
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
        IParameterSymbol previousParameter,
        IParameterSymbol? resultParameter,
        LambdaExpressionSyntax transferScope,
        IReadOnlyDictionary<ISymbol, string> localSubstitutions,
        IReadOnlyDictionary<ISymbol, ExpressionSyntax> localInitializers,
        CancellationToken cancellationToken,
        out ConventionMemberMappingPlan plan,
        out string? unsupportedMessage)
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
        var hasExplicitCreationOnlyMappings = false;
        var hasResultDependentCreationOnlyMappings = false;
        var createNestedMapUsages =
            new DeclarativeNestedMapUsageRegistry();
        var mapReplacementNestedMapUsages =
            new DeclarativeNestedMapUsageRegistry();
        var updateNestedMapUsages =
            new DeclarativeNestedMapUsageRegistry();

        foreach (var assignment in assignments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!occupiedNames.Add(assignment.MemberName) ||
                !writableMembersByName.TryGetValue(
                    assignment.MemberName,
                    out var destinationMember))
            {
                plan = default;
                unsupportedMessage = UnsupportedMembersMessage;
                return false;
            }

            if (DeclarativeMemberMarker.TryGetKind(
                    assignment.Value,
                    semanticModel,
                    cancellationToken,
                    out var markerKind))
            {
                if (markerKind == DeclarativeMemberMarkerKind.Ignore)
                {
                    continue;
                }

                if (!conventionCreateByName.TryGetValue(
                        destinationMember.Name,
                        out var automaticCreate))
                {
                    plan = default;
                    unsupportedMessage =
                        AutomaticMemberUnavailableMessage;
                    return false;
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

                hasExplicitCreationOnlyMappings |=
                    !destinationMember.CanAssign;
                continue;
            }

            if (!TryBuildExplicitMapping(
                    assignment.Value,
                    destinationMember,
                    mapping,
                    semanticModel,
                    mapperType,
                    sourceParameter,
                    previousParameter,
                    resultParameter,
                    transferScope,
                    localSubstitutions,
                    localInitializers,
                    createNestedMapUsages,
                    mapReplacementNestedMapUsages,
                    updateNestedMapUsages,
                    cancellationToken,
                    out var explicitPlan))
            {
                plan = default;
                unsupportedMessage = UnsupportedMembersMessage;
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

            hasExplicitCreationOnlyMappings |=
                explicitPlan.IsCreationOnly;
            hasResultDependentCreationOnlyMappings |=
                explicitPlan.IsCreationOnly &&
                explicitPlan.IsResultDependent;
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
        }

        var immutableCreate = create.ToImmutable();

        plan = new ConventionMemberMappingPlan(
            immutableCreate,
            createPost.ToImmutable(),
            mapReplacement.ToImmutable(),
            mapReplacementPost.ToImmutable(),
            update.ToImmutable(),
            ConventionMemberMappingPlanner.HasUnmappedRequiredMembers(
                destination,
                immutableCreate,
                cancellationToken),
            hasExplicitCreationOnlyMappings,
            hasResultDependentCreationOnlyMappings,
            occupiedNames.ToImmutableArray());
        unsupportedMessage = null;
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
        var writableMembers =
            ConventionMemberMappingPlanner.BuildWritableMembers(
                destination,
                capabilities,
                compilation,
                cancellationToken)
            .ToDictionary(
                static member => member.Name,
                StringComparer.Ordinal);
        var creationOnlyMappings = create
            .Where(mapping =>
                occupiedNames.Contains(mapping.DestinationMemberName) &&
                writableMembers.TryGetValue(
                    mapping.DestinationMemberName,
                    out var member) &&
                !member.CanAssign)
            .ToArray();

        return new ConventionMemberMappingPlan(
            create,
            createPost,
            mapReplacement,
            mapReplacementPost,
            update,
            ConventionMemberMappingPlanner.HasUnmappedRequiredMembers(
                destination,
                create,
                cancellationToken),
            creationOnlyMappings.Length > 0,
            creationOnlyMappings.Any(static mapping =>
                mapping.IsResultDependent),
            occupiedNames.ToImmutableArray());
    }

    private static bool TryBuildExplicitMapping(
        ExpressionSyntax expression,
        ConventionWritableMember destinationMember,
        TypeMapperMappingModel mapping,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        IParameterSymbol previousParameter,
        IParameterSymbol? resultParameter,
        LambdaExpressionSyntax transferScope,
        IReadOnlyDictionary<ISymbol, string> localSubstitutions,
        IReadOnlyDictionary<ISymbol, ExpressionSyntax> localInitializers,
        DeclarativeNestedMapUsageRegistry createNestedMapUsages,
        DeclarativeNestedMapUsageRegistry mapReplacementNestedMapUsages,
        DeclarativeNestedMapUsageRegistry updateNestedMapUsages,
        CancellationToken cancellationToken,
        out ExplicitMemberMappingPlan plan)
    {
        if (!DeclarativeDependencyExpressionBuilder.TryRewrite(
                expression,
                semanticModel,
                mapperType,
                sourceParameter,
                mapping.NonNullSourceName,
                previousParameter,
                BuildPreviousSubstitution(mapping, hasPrevious: false),
                resultParameter,
                mapping.ResultLocalName,
                transferScope,
                localSubstitutions,
                destinationMember.Type,
                new DeclarativeNestedMapTargetContext(
                    destinationMember.Type,
                    destinationMember.Name,
                    DeclarativeNestedMapOperation.Create,
                    CurrentDestinationExpression: null),
                createNestedMapUsages,
                cancellationToken,
                out var createExpression,
                out var createDependency) ||
            !DeclarativeDependencyExpressionBuilder.TryRewrite(
                expression,
                semanticModel,
                mapperType,
                sourceParameter,
                mapping.NonNullSourceName,
                previousParameter,
                BuildPreviousSubstitution(mapping, hasPrevious: true),
                resultParameter,
                mapping.ResultLocalName,
                transferScope,
                localSubstitutions,
                destinationMember.Type,
                new DeclarativeNestedMapTargetContext(
                    destinationMember.Type,
                    destinationMember.Name,
                    DeclarativeNestedMapOperation.Update,
                    mapping.ResultLocalName + "." +
                    Identifier(destinationMember.Name)),
                mapReplacementNestedMapUsages,
                cancellationToken,
                out var mapReplacementExpression,
                out var mapReplacementDependency) ||
            !DeclarativeDependencyExpressionBuilder.TryRewrite(
                expression,
                semanticModel,
                mapperType,
                sourceParameter,
                mapping.NonNullSourceName,
                previousParameter,
                BuildPreviousSubstitution(mapping, hasPrevious: true),
                resultParameter,
                "destination",
                transferScope,
                localSubstitutions,
                destinationMember.Type,
                new DeclarativeNestedMapTargetContext(
                    destinationMember.Type,
                    destinationMember.Name,
                    DeclarativeNestedMapOperation.Update,
                    "destination." +
                    Identifier(destinationMember.Name)),
                updateNestedMapUsages,
                cancellationToken,
                out var updateExpression,
                out var updateDependency))
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
                destinationMember.Type);
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

        var create = BuildMapping(
            createExpression,
            createDependency,
            isResultDependent);
        var replacementIsResultDependent =
            isResultDependent ||
            ReferencesIdentifier(
                mapReplacementExpression,
                mapping.ResultLocalName);
        var mapReplacement = BuildMapping(
            mapReplacementExpression,
            mapReplacementDependency,
            replacementIsResultDependent);
        var update = BuildMapping(
            updateExpression,
            updateDependency,
            isResultDependent);

        plan = new ExplicitMemberMappingPlan(
            Create: isResultDependent ? null : create,
            CreatePost: destinationMember.CanAssign
                ? create
                : null,
            MapReplacement: replacementIsResultDependent
                ? null
                : mapReplacement,
            MapReplacementPost: destinationMember.CanAssign
                ? mapReplacement
                : null,
            Update: destinationMember.CanAssign
                ? update
                : null,
            IsCreationOnly: !destinationMember.CanAssign,
            IsResultDependent: isResultDependent);
        return true;
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
        out IParameterSymbol previousParameter,
        out IParameterSymbol? resultParameter)
    {
        var expectedCount = form ==
            MembersConfigurationForm.SourceAndPrevious
                ? 2
                : 3;

        if (lambda is not ParenthesizedLambdaExpressionSyntax parenthesized ||
            parenthesized.ParameterList.Parameters.Count != expectedCount ||
            semanticModel.GetDeclaredSymbol(
                    parenthesized.ParameterList.Parameters[0],
                    cancellationToken) is not
                    IParameterSymbol resolvedSource ||
            semanticModel.GetDeclaredSymbol(
                    parenthesized.ParameterList.Parameters[1],
                    cancellationToken) is not
                    IParameterSymbol resolvedPrevious)
        {
            sourceParameter = null!;
            previousParameter = null!;
            resultParameter = null;
            return false;
        }

        sourceParameter = resolvedSource;
        previousParameter = resolvedPrevious;
        resultParameter = expectedCount == 3
            ? semanticModel.GetDeclaredSymbol(
                    parenthesized.ParameterList.Parameters[2],
                    cancellationToken) as IParameterSymbol
            : null;
        return expectedCount == 2 || resultParameter is not null;
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
}

internal readonly record struct BasicMembersMappingResult(
    ConventionMemberMappingPlan Plan,
    MembersDeclarativeControlFlowPlan? ControlFlow,
    string? UnsupportedMessage)
{
    public static BasicMembersMappingResult Unsupported(string message) =>
        new(default, ControlFlow: null, message);
}

internal sealed record MembersDeclarativeControlFlowPlan(
    DeclarativeControlFlowProgram Program,
    IReadOnlyDictionary<
        DeclarativeLeafSyntaxNode,
        ConventionMemberMappingPlan> Leaves,
    SemanticModel SemanticModel,
    INamedTypeSymbol MapperType,
    IParameterSymbol SourceParameter,
    IParameterSymbol PreviousParameter,
    IParameterSymbol? ResultParameter,
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
