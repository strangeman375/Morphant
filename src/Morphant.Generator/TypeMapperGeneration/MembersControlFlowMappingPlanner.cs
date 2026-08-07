using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MembersControlFlowMappingPlanner
{
    private const string UnsupportedMembersMessage =
        "The configured Members control flow cannot be represented by the " +
        "supported declarative grammar.";

    public static TypeMapperMappingModel Build(
        MembersDeclarativeControlFlowPlan members,
        TypeMapperMappingModel mapping,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        HashSet<string> usedGeneratedMethodNames,
        bool directConstruction,
        Func<
            ConventionMemberMappingPlan,
            ByFactoryHelperRegistry?,
            TypeMapperMappingModel> buildFlatMapping,
        CancellationToken cancellationToken)
    {
        var hasResultDependentControlFlow =
            ContainsReadOnlyMemberUpdate(
                members.Program.Root,
                members.SemanticModel,
                cancellationToken) ||
            members.ResultParameter is { } resultParameter &&
            ReferencesResultInControlFlow(
                members.Program.Root,
                resultParameter,
                members.SemanticModel,
                members.LocalInitializers,
                members.Program.RuntimeLocalPlaceholders,
                cancellationToken);

        var sharedFactoryHelpers = directConstruction
            ? null
            : new ByFactoryHelperRegistry(usedGeneratedMethodNames);
        var flatMappings =
            new Dictionary<
                DeclarativeLeafSyntaxNode,
                TypeMapperMappingModel>();
        var helperDeclarations =
            ImmutableArray.CreateBuilder<string>();
        var seenHelpers = new HashSet<string>(StringComparer.Ordinal);
        TypeMapperMappingModel? sharedDirectMapping = null;

        foreach (var leaf in members.Leaves)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var flat = sharedDirectMapping is { } directMapping
                ? ApplyMemberPlan(
                    directMapping,
                    leaf.Value,
                    mapperType)
                : buildFlatMapping(
                    leaf.Value,
                    sharedFactoryHelpers);

            if (flat.UnsupportedExceptionMessage is { } unsupported)
            {
                sharedFactoryHelpers?.Rollback();
                return mapping with
                {
                    UnsupportedExceptionMessage = unsupported
                };
            }

            flatMappings.Add(leaf.Key, flat);

            if (directConstruction &&
                sharedDirectMapping is null &&
                flat.ControlFlow is not null)
            {
                sharedDirectMapping = flat;
            }

            foreach (var declaration in
                     flat.HelperMethodDeclarations.IsDefault
                         ? []
                         : flat.HelperMethodDeclarations)
            {
                if (seenHelpers.Add(declaration))
                {
                    helperDeclarations.Add(declaration);
                }
            }
        }

        if (sharedFactoryHelpers is not null)
        {
            foreach (var declaration in
                     sharedFactoryHelpers.HelperMethodDeclarations)
            {
                if (seenHelpers.Add(declaration))
                {
                    helperDeclarations.Add(declaration);
                }
            }
        }

        if (hasResultDependentControlFlow)
        {
            if (!TryBuildResultDependentControlFlow(
                    members,
                    mapping,
                    flatMappings,
                    compilation,
                    mapperType,
                    cancellationToken,
                    out var resultDependentControlFlow))
            {
                sharedFactoryHelpers?.Rollback();
                return mapping with
                {
                    UnsupportedExceptionMessage =
                        UnsupportedMembersMessage
                };
            }

            var resultRepresentative = flatMappings.Values.First();

            return resultRepresentative with
            {
                ControlFlow = resultDependentControlFlow,
                HelperMethodDeclarations = helperDeclarations.ToImmutable(),
                UnsupportedExceptionMessage = null
            };
        }

        TypeMapperControlFlowNode? BuildCreateLeaf(
            DeclarativeLeafSyntaxNode leaf)
        {
            return SelectRoot(
                flatMappings[leaf],
                create: true);
        }

        TypeMapperControlFlowNode? BuildUpdateLeaf(
            DeclarativeLeafSyntaxNode leaf)
        {
            return SelectRoot(
                flatMappings[leaf],
                create: false);
        }

        if (!DeclarativeControlFlowLowerer.TryBuild(
                members.Program,
                members.SemanticModel,
                compilation,
                mapperType,
                members.SourceParameter,
                mapping.NonNullSourceName,
                members.PreviousParameter,
                BuildPreviousSubstitution(mapping, hasPrevious: false),
                members.ResultParameter,
                mapping.ResultLocalName,
                members.TransferScope,
                BuildCreateLeaf,
                cancellationToken,
                out var createRoot) ||
            !DeclarativeControlFlowLowerer.TryBuild(
                members.Program,
                members.SemanticModel,
                compilation,
                mapperType,
                members.SourceParameter,
                mapping.NonNullSourceName,
                members.PreviousParameter,
                BuildPreviousSubstitution(mapping, hasPrevious: true),
                members.ResultParameter,
                "destination",
                members.TransferScope,
                BuildUpdateLeaf,
                cancellationToken,
                out var updateRoot))
        {
            sharedFactoryHelpers?.Rollback();
            return mapping with
            {
                UnsupportedExceptionMessage =
                    UnsupportedMembersMessage
            };
        }

        var representative = flatMappings.Values.First();

        return representative with
        {
            ControlFlow = new TypeMapperControlFlowMappingModel(
                createRoot,
                updateRoot),
            HelperMethodDeclarations = helperDeclarations.ToImmutable(),
            UnsupportedExceptionMessage = null
        };
    }

    private static TypeMapperMappingModel ApplyMemberPlan(
        TypeMapperMappingModel template,
        ConventionMemberMappingPlan memberPlan,
        INamedTypeSymbol mapperType)
    {
        if (template.ControlFlow is not { } controlFlow)
        {
            return template;
        }

        TypeMapperControlFlowNode Apply(
            TypeMapperControlFlowNode node,
            bool create)
        {
            if (node.EvaluationContinuation is
                    { } evaluationContinuation)
            {
                return node with
                {
                    EvaluationContinuation = Apply(
                        evaluationContinuation,
                        create)
                };
            }

            if (node.SwitchExpression is not null)
            {
                return node with
                {
                    SwitchSections = node.SwitchSections.Select(section =>
                            section with
                            {
                                Branch = Apply(
                                    section.Branch,
                                    create)
                            })
                        .ToImmutableArray(),
                    SwitchContinuation = node.SwitchContinuation is
                        { } continuation
                        ? Apply(continuation, create)
                        : null
                };
            }

            if (node.Condition is not null)
            {
                return node with
                {
                    WhenTrue = Apply(node.WhenTrue!, create),
                    WhenFalse = Apply(node.WhenFalse!, create)
                };
            }

            if (node.Leaf is not { } leaf)
            {
                return node;
            }

            var replacement = !create &&
                (leaf.CreateFactory is not null ||
                 leaf.CreateConstructor is not null);
            var postMappings = create
                ? memberPlan.CreatePost
                : replacement
                    ? memberPlan.MapReplacementPost
                    : [];
            var factory = leaf.CreateFactory;

            if (factory is { } factoryValue)
            {
                factory = UserResultMappingPlanner.BuildFactoryMapping(
                    leaf,
                    postMappings,
                    mapperType,
                    factoryValue.ValueExpression);
            }

            return node with
            {
                Leaf = leaf with
                {
                    CreateFactory = factory,
                    CreateMemberMappings = [],
                    CreatePostMemberMappings = postMappings,
                    UpdateMemberMappings = create || replacement
                        ? []
                        : memberPlan.Update
                }
            };
        }

        return template with
        {
            ControlFlow = new TypeMapperControlFlowMappingModel(
                Apply(controlFlow.CreateRoot, create: true),
                Apply(controlFlow.UpdateRoot, create: false))
        };
    }

    private static bool TryBuildResultDependentControlFlow(
        MembersDeclarativeControlFlowPlan members,
        TypeMapperMappingModel mapping,
        IReadOnlyDictionary<
            DeclarativeLeafSyntaxNode,
            TypeMapperMappingModel> flatMappings,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken,
        out TypeMapperControlFlowMappingModel controlFlow)
    {
        var resultParameter = members.ResultParameter;

        if (!TryBuildPostControlFlow(
                members,
                mapping,
                compilation,
                mapperType,
                BuildPreviousSubstitution(mapping, hasPrevious: false),
                mapping.ResultLocalName,
                static plan => plan.CreatePost,
                cancellationToken,
                out var createPost) ||
            !TryBuildPostControlFlow(
                members,
                mapping,
                compilation,
                mapperType,
                BuildPreviousSubstitution(mapping, hasPrevious: true),
                mapping.ResultLocalName,
                static plan => plan.MapReplacementPost,
                cancellationToken,
                out var replacementPost) ||
            !TryBuildPostControlFlow(
                members,
                mapping,
                compilation,
                mapperType,
                BuildPreviousSubstitution(mapping, hasPrevious: true),
                "destination",
                static plan => plan.Update,
                cancellationToken,
                out var existingPost))
        {
            controlFlow = null!;
            return false;
        }

        var createAssignableNames = new HashSet<string>(
            members.Leaves.Values
                .SelectMany(static plan => plan.CreatePost)
                .Select(static member =>
                    member.DestinationMemberName),
            StringComparer.Ordinal);
        var replacementAssignableNames = new HashSet<string>(
            members.Leaves.Values
                .SelectMany(static plan => plan.MapReplacementPost)
                .Select(static member =>
                    member.DestinationMemberName),
            StringComparer.Ordinal);
        TypeMapperControlFlowNode? selectedCreate = null;
        TypeMapperControlFlowNode? selectedUpdate = null;

        foreach (var flat in flatMappings.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var preparedCreate = PrepareConstructionRoot(
                SelectRoot(flat, create: true),
                create: true,
                createPost,
                replacementPost,
                existingPost,
                createAssignableNames,
                replacementAssignableNames,
                mapperType);
            var preparedUpdate = PrepareConstructionRoot(
                SelectRoot(flat, create: false),
                create: false,
                createPost,
                replacementPost,
                existingPost,
                createAssignableNames,
                replacementAssignableNames,
                mapperType);

            if (selectedCreate is null)
            {
                selectedCreate = preparedCreate;
                selectedUpdate = preparedUpdate;
                continue;
            }

            if (!AreEquivalentConstruction(
                    selectedCreate,
                    preparedCreate) ||
                !AreEquivalentConstruction(
                    selectedUpdate!,
                    preparedUpdate))
            {
                controlFlow = null!;
                return false;
            }
        }

        if (selectedCreate is null || selectedUpdate is null)
        {
            controlFlow = null!;
            return false;
        }

        controlFlow = new TypeMapperControlFlowMappingModel(
            selectedCreate,
            selectedUpdate);
        return true;

        bool TryBuildPostControlFlow(
            MembersDeclarativeControlFlowPlan configuredMembers,
            TypeMapperMappingModel baseMapping,
            CSharpCompilation currentCompilation,
            INamedTypeSymbol currentMapperType,
            PreviousExpressionSubstitution previousSubstitution,
            string resultName,
            Func<ConventionMemberMappingPlan,
                ImmutableArray<TypeMapperMemberMappingModel>> selectMembers,
            CancellationToken currentCancellationToken,
            out TypeMapperMemberControlFlowNode root)
        {
            return DeclarativeControlFlowLowerer
                .TryBuildMemberControlFlow(
                    configuredMembers.Program,
                    configuredMembers.SemanticModel,
                    currentCompilation,
                    currentMapperType,
                    configuredMembers.SourceParameter,
                    baseMapping.NonNullSourceName,
                    configuredMembers.PreviousParameter,
                    previousSubstitution,
                    resultParameter,
                    resultName,
                    configuredMembers.TransferScope,
                    baseMapping,
                    leaf => selectMembers(
                        configuredMembers.Leaves[leaf]),
                    currentCancellationToken,
                    out root);
        }
    }

    private static TypeMapperControlFlowNode PrepareConstructionRoot(
        TypeMapperControlFlowNode node,
        bool create,
        TypeMapperMemberControlFlowNode createPost,
        TypeMapperMemberControlFlowNode replacementPost,
        TypeMapperMemberControlFlowNode existingPost,
        HashSet<string> createAssignableNames,
        HashSet<string> replacementAssignableNames,
        INamedTypeSymbol mapperType)
    {
        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            return node with
            {
                EvaluationContinuation = PrepareConstructionRoot(
                    evaluationContinuation,
                    create,
                    createPost,
                    replacementPost,
                    existingPost,
                    createAssignableNames,
                    replacementAssignableNames,
                    mapperType)
            };
        }

        if (node.SwitchExpression is not null)
        {
            return node with
            {
                SwitchSections = node.SwitchSections.Select(section =>
                        section with
                        {
                            Branch = PrepareConstructionRoot(
                                section.Branch,
                                create,
                                createPost,
                                replacementPost,
                                existingPost,
                                createAssignableNames,
                                replacementAssignableNames,
                                mapperType)
                        })
                    .ToImmutableArray(),
                SwitchContinuation = node.SwitchContinuation is
                    { } continuation
                    ? PrepareConstructionRoot(
                        continuation,
                        create,
                        createPost,
                        replacementPost,
                        existingPost,
                        createAssignableNames,
                        replacementAssignableNames,
                        mapperType)
                    : null
            };
        }

        if (node.Condition is not null)
        {
            return node with
            {
                WhenTrue = PrepareConstructionRoot(
                    node.WhenTrue!,
                    create,
                    createPost,
                    replacementPost,
                    existingPost,
                    createAssignableNames,
                    replacementAssignableNames,
                    mapperType),
                WhenFalse = PrepareConstructionRoot(
                    node.WhenFalse!,
                    create,
                    createPost,
                    replacementPost,
                    existingPost,
                    createAssignableNames,
                    replacementAssignableNames,
                    mapperType)
            };
        }

        if (node.Leaf is not { } leaf)
        {
            return node;
        }

        var replacement = !create &&
            (leaf.CreateFactory is not null ||
             leaf.CreateConstructor is not null);
        var post = create
            ? createPost
            : replacement
                ? replacementPost
                : existingPost;
        var assignableNames = replacement
            ? replacementAssignableNames
            : createAssignableNames;
        var factory = leaf.CreateFactory;

        if (factory is { } factoryValue)
        {
            factory = UserResultMappingPlanner.BuildFactoryMapping(
                leaf,
                [default],
                mapperType,
                factoryValue.ValueExpression);
        }

        return node with
        {
            Leaf = leaf with
            {
                CreateFactory = factory,
                CreateMemberMappings = create || replacement
                    ? leaf.CreateMemberMappings.Where(member =>
                            !assignableNames.Contains(
                                member.DestinationMemberName))
                        .ToImmutableArray()
                    : [],
                CreatePostMemberMappings = [],
                UpdateMemberMappings = [],
                PostMemberControlFlow = post
            }
        };
    }

    private static bool AreEquivalentConstruction(
        TypeMapperControlFlowNode left,
        TypeMapperControlFlowNode right)
    {
        if (!left.Locals.SequenceEqual(right.Locals) ||
            !StringComparer.Ordinal.Equals(
                left.Condition,
                right.Condition) ||
            !StringComparer.Ordinal.Equals(
                left.ThrowExpression,
                right.ThrowExpression) ||
            !StringComparer.Ordinal.Equals(
                left.SwitchExpression,
                right.SwitchExpression) ||
            !StringComparer.Ordinal.Equals(
                left.EvaluationExpression,
                right.EvaluationExpression) ||
            left.SwitchRequiresFallback !=
                right.SwitchRequiresFallback ||
            left.SwitchCanPassUnmatchedValue !=
                right.SwitchCanPassUnmatchedValue)
        {
            return false;
        }

        if (left.Leaf.HasValue || right.Leaf.HasValue)
        {
            return left.Leaf.HasValue &&
                   right.Leaf.HasValue &&
                   AreEquivalentConstructionLeaf(
                       left.Leaf.Value,
                       right.Leaf.Value);
        }

        if (left.EvaluationContinuation is not null ||
            right.EvaluationContinuation is not null)
        {
            return left.EvaluationContinuation is
                       { } leftEvaluation &&
                   right.EvaluationContinuation is
                       { } rightEvaluation &&
                   AreEquivalentConstruction(
                       leftEvaluation,
                       rightEvaluation);
        }

        if (left.SwitchExpression is not null)
        {
            if (right.SwitchExpression is null ||
                left.SwitchSections.Length !=
                    right.SwitchSections.Length)
            {
                return false;
            }

            for (var index = 0;
                 index < left.SwitchSections.Length;
                 index++)
            {
                var leftSection = left.SwitchSections[index];
                var rightSection = right.SwitchSections[index];

                if (!leftSection.Labels.SequenceEqual(
                        rightSection.Labels,
                        StringComparer.Ordinal) ||
                    !AreEquivalentConstruction(
                        leftSection.Branch,
                        rightSection.Branch))
                {
                    return false;
                }
            }

            return left.SwitchContinuation is null
                ? right.SwitchContinuation is null
                : right.SwitchContinuation is
                    { } rightContinuation &&
                  AreEquivalentConstruction(
                      left.SwitchContinuation,
                      rightContinuation);
        }

        if (left.Condition is not null)
        {
            return right.Condition is not null &&
                   AreEquivalentConstruction(
                       left.WhenTrue!,
                       right.WhenTrue!) &&
                   AreEquivalentConstruction(
                       left.WhenFalse!,
                       right.WhenFalse!);
        }

        return right.Leaf is null &&
               right.EvaluationContinuation is null &&
               right.SwitchExpression is null &&
               right.Condition is null;
    }

    private static bool AreEquivalentConstructionLeaf(
        TypeMapperMappingModel left,
        TypeMapperMappingModel right)
    {
        return StringComparer.Ordinal.Equals(
                   left.CreateDirectExpression,
                   right.CreateDirectExpression) &&
               StringComparer.Ordinal.Equals(
                   left.UpdateDirectExpression,
                   right.UpdateDirectExpression) &&
               Nullable.Equals(
                   left.CreateFactory,
                   right.CreateFactory) &&
               AreEquivalentConstructor(
                   left.CreateConstructor,
                   right.CreateConstructor) &&
               left.CreateMemberMappings.SequenceEqual(
                   right.CreateMemberMappings) &&
               StringComparer.Ordinal.Equals(
                   left.CreateUnsupportedExceptionMessage,
                   right.CreateUnsupportedExceptionMessage) &&
               StringComparer.Ordinal.Equals(
                   left.UpdateUnsupportedExceptionMessage,
                   right.UpdateUnsupportedExceptionMessage) &&
               StringComparer.Ordinal.Equals(
                   left.UnsupportedExceptionMessage,
                   right.UnsupportedExceptionMessage);
    }

    private static bool AreEquivalentConstructor(
        TypeMapperConstructorMappingModel? left,
        TypeMapperConstructorMappingModel? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return StringComparer.Ordinal.Equals(
                   left.Value.ConstructedTypeName,
                   right.Value.ConstructedTypeName) &&
               left.Value.Arguments.SequenceEqual(
                   right.Value.Arguments);
    }

    private static TypeMapperControlFlowNode SelectRoot(
        TypeMapperMappingModel mapping,
        bool create)
    {
        if (mapping.ControlFlow is { } controlFlow)
        {
            return create
                ? controlFlow.CreateRoot
                : controlFlow.UpdateRoot;
        }

        var leaf = create
            ? mapping
            : mapping with
            {
                CreateFactory = null,
                CreateConstructor = null
            };

        return new TypeMapperControlFlowNode(
            Locals: [],
            Condition: null,
            WhenTrue: null,
            WhenFalse: null,
            leaf with { ControlFlow = null },
            ThrowExpression: null);
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

    private static bool ReferencesResultInControlFlow(
        DeclarativeControlFlowSyntaxNode node,
        IParameterSymbol resultParameter,
        SemanticModel semanticModel,
        IReadOnlyDictionary<ISymbol, ExpressionSyntax> localInitializers,
        IReadOnlyDictionary<ISymbol, string> localPlaceholders,
        CancellationToken cancellationToken)
    {
        bool References(ExpressionSyntax expression) =>
            ReferencesParameter(
                expression,
                resultParameter,
                semanticModel,
                localInitializers,
                new HashSet<ISymbol>(
                    SymbolEqualityComparer.Default),
                cancellationToken);

        switch (node)
        {
            case DeclarativeLeafSyntaxNode:
                return false;

            case DeclarativeThrowSyntaxNode throwNode:
                return References(throwNode.Expression);

            case DeclarativeLocalDeclarationsSyntaxNode locals:
                return locals.RuntimeLocalPlaceholders.Any(placeholder =>
                           localPlaceholders.Any(pair =>
                               StringComparer.Ordinal.Equals(
                                   pair.Value,
                                   placeholder) &&
                               localInitializers.TryGetValue(
                                   pair.Key,
                                   out var initializer) &&
                               References(initializer))) ||
                       ReferencesResultInControlFlow(
                           locals.Next,
                           resultParameter,
                           semanticModel,
                           localInitializers,
                           localPlaceholders,
                           cancellationToken);

            case DeclarativeEvaluationSyntaxNode evaluation:
                return References(evaluation.Expression) ||
                       ReferencesResultInControlFlow(
                           evaluation.Next,
                           resultParameter,
                           semanticModel,
                           localInitializers,
                           localPlaceholders,
                           cancellationToken);

            case DeclarativeConditionalSyntaxNode conditional:
                return References(conditional.Condition) ||
                       ReferencesResultInControlFlow(
                           conditional.WhenTrue,
                           resultParameter,
                           semanticModel,
                           localInitializers,
                           localPlaceholders,
                           cancellationToken) ||
                       ReferencesResultInControlFlow(
                           conditional.WhenFalse,
                           resultParameter,
                           semanticModel,
                           localInitializers,
                           localPlaceholders,
                           cancellationToken);

            case DeclarativeSwitchSyntaxNode switchNode:
                return References(switchNode.GoverningExpression) ||
                       switchNode.Sections.Any(section =>
                           section.Labels.Any(label =>
                               label.Value is { } value &&
                               References(value) ||
                               label.WhenCondition is { } condition &&
                               References(condition)) ||
                           ReferencesResultInControlFlow(
                               section.Branch,
                               resultParameter,
                               semanticModel,
                               localInitializers,
                               localPlaceholders,
                               cancellationToken)) ||
                       switchNode.Continuation is { } continuation &&
                       ReferencesResultInControlFlow(
                           continuation,
                           resultParameter,
                           semanticModel,
                           localInitializers,
                           localPlaceholders,
                           cancellationToken);

            default:
                return false;
        }

    }

    private static bool ContainsReadOnlyMemberUpdate(
        DeclarativeControlFlowSyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return node switch
        {
            DeclarativeEvaluationSyntaxNode evaluation =>
                DeclarativeNestedMapExpression
                    .IsReadOnlyMemberUpdateStatement(
                        evaluation.Expression,
                        semanticModel,
                        cancellationToken) ||
                ContainsReadOnlyMemberUpdate(
                    evaluation.Next,
                    semanticModel,
                    cancellationToken),
            DeclarativeLocalDeclarationsSyntaxNode locals =>
                ContainsReadOnlyMemberUpdate(
                    locals.Next,
                    semanticModel,
                    cancellationToken),
            DeclarativeConditionalSyntaxNode conditional =>
                ContainsReadOnlyMemberUpdate(
                    conditional.WhenTrue,
                    semanticModel,
                    cancellationToken) ||
                ContainsReadOnlyMemberUpdate(
                    conditional.WhenFalse,
                    semanticModel,
                    cancellationToken),
            DeclarativeSwitchSyntaxNode switchNode =>
                switchNode.Sections.Any(section =>
                    ContainsReadOnlyMemberUpdate(
                        section.Branch,
                        semanticModel,
                        cancellationToken)) ||
                switchNode.Continuation is { } continuation &&
                ContainsReadOnlyMemberUpdate(
                    continuation,
                    semanticModel,
                    cancellationToken),
            _ => false
        };
    }

    private static bool ReferencesParameter(
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
            var symbol = semanticModel.GetSymbolInfo(
                    identifier,
                    cancellationToken)
                .Symbol;

            if (SymbolEqualityComparer.Default.Equals(
                    symbol,
                    parameter))
            {
                return true;
            }

            if (symbol is not null &&
                visitedLocals.Add(symbol) &&
                localInitializers.TryGetValue(
                    symbol,
                    out var initializer) &&
                ReferencesParameter(
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
}
