using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MembersControlFlowMappingPlanner
{
    private const string UnsupportedMembersMessage =
        "The configured Members control flow is not supported yet.";

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

        TypeMapperControlFlowNode? BuildMapNewLeaf(
            DeclarativeLeafSyntaxNode leaf)
        {
            return SelectRoot(
                flatMappings[leaf],
                mapNew: true);
        }

        TypeMapperControlFlowNode? BuildMapExistingLeaf(
            DeclarativeLeafSyntaxNode leaf)
        {
            return SelectRoot(
                flatMappings[leaf],
                mapNew: false);
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
                BuildMapNewLeaf,
                cancellationToken,
                out var mapNewRoot) ||
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
                BuildMapExistingLeaf,
                cancellationToken,
                out var mapExistingRoot))
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
                mapNewRoot,
                mapExistingRoot),
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
            bool mapNew)
        {
            if (node.EvaluationContinuation is
                    { } evaluationContinuation)
            {
                return node with
                {
                    EvaluationContinuation = Apply(
                        evaluationContinuation,
                        mapNew)
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
                                    mapNew)
                            })
                        .ToImmutableArray(),
                    SwitchContinuation = node.SwitchContinuation is
                        { } continuation
                        ? Apply(continuation, mapNew)
                        : null
                };
            }

            if (node.Condition is not null)
            {
                return node with
                {
                    WhenTrue = Apply(node.WhenTrue!, mapNew),
                    WhenFalse = Apply(node.WhenFalse!, mapNew)
                };
            }

            if (node.Leaf is not { } leaf)
            {
                return node;
            }

            var replacement = !mapNew &&
                (leaf.MapNewFactory is not null ||
                 leaf.MapNewConstructor is not null);
            var postMappings = mapNew
                ? memberPlan.MapNewPost
                : replacement
                    ? memberPlan.MapReplacementPost
                    : [];
            var factory = leaf.MapNewFactory;

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
                    MapNewFactory = factory,
                    MapNewMemberMappings = [],
                    MapNewPostMemberMappings = postMappings,
                    MapExistingMemberMappings = mapNew || replacement
                        ? []
                        : memberPlan.MapExisting
                }
            };
        }

        return template with
        {
            ControlFlow = new TypeMapperControlFlowMappingModel(
                Apply(controlFlow.MapNewRoot, mapNew: true),
                Apply(controlFlow.MapExistingRoot, mapNew: false))
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
                static plan => plan.MapNewPost,
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
                static plan => plan.MapExisting,
                cancellationToken,
                out var existingPost))
        {
            controlFlow = null!;
            return false;
        }

        var createAssignableNames = new HashSet<string>(
            members.Leaves.Values
                .SelectMany(static plan => plan.MapNewPost)
                .Select(static member =>
                    member.DestinationMemberName),
            StringComparer.Ordinal);
        var replacementAssignableNames = new HashSet<string>(
            members.Leaves.Values
                .SelectMany(static plan => plan.MapReplacementPost)
                .Select(static member =>
                    member.DestinationMemberName),
            StringComparer.Ordinal);
        TypeMapperControlFlowNode? selectedMapNew = null;
        TypeMapperControlFlowNode? selectedMapExisting = null;

        foreach (var flat in flatMappings.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var preparedMapNew = PrepareConstructionRoot(
                SelectRoot(flat, mapNew: true),
                mapNew: true,
                createPost,
                replacementPost,
                existingPost,
                createAssignableNames,
                replacementAssignableNames,
                mapperType);
            var preparedMapExisting = PrepareConstructionRoot(
                SelectRoot(flat, mapNew: false),
                mapNew: false,
                createPost,
                replacementPost,
                existingPost,
                createAssignableNames,
                replacementAssignableNames,
                mapperType);

            if (selectedMapNew is null)
            {
                selectedMapNew = preparedMapNew;
                selectedMapExisting = preparedMapExisting;
                continue;
            }

            if (!AreEquivalentConstruction(
                    selectedMapNew,
                    preparedMapNew) ||
                !AreEquivalentConstruction(
                    selectedMapExisting!,
                    preparedMapExisting))
            {
                controlFlow = null!;
                return false;
            }
        }

        if (selectedMapNew is null || selectedMapExisting is null)
        {
            controlFlow = null!;
            return false;
        }

        controlFlow = new TypeMapperControlFlowMappingModel(
            selectedMapNew,
            selectedMapExisting);
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
        bool mapNew,
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
                    mapNew,
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
                                mapNew,
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
                        mapNew,
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
                    mapNew,
                    createPost,
                    replacementPost,
                    existingPost,
                    createAssignableNames,
                    replacementAssignableNames,
                    mapperType),
                WhenFalse = PrepareConstructionRoot(
                    node.WhenFalse!,
                    mapNew,
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

        var replacement = !mapNew &&
            (leaf.MapNewFactory is not null ||
             leaf.MapNewConstructor is not null);
        var post = mapNew
            ? createPost
            : replacement
                ? replacementPost
                : existingPost;
        var assignableNames = replacement
            ? replacementAssignableNames
            : createAssignableNames;
        var factory = leaf.MapNewFactory;

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
                MapNewFactory = factory,
                MapNewMemberMappings = mapNew || replacement
                    ? leaf.MapNewMemberMappings.Where(member =>
                            !assignableNames.Contains(
                                member.DestinationMemberName))
                        .ToImmutableArray()
                    : [],
                MapNewPostMemberMappings = [],
                MapExistingMemberMappings = [],
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
                   left.MapNewDirectExpression,
                   right.MapNewDirectExpression) &&
               StringComparer.Ordinal.Equals(
                   left.MapExistingDirectExpression,
                   right.MapExistingDirectExpression) &&
               Nullable.Equals(
                   left.MapNewFactory,
                   right.MapNewFactory) &&
               AreEquivalentConstructor(
                   left.MapNewConstructor,
                   right.MapNewConstructor) &&
               left.MapNewMemberMappings.SequenceEqual(
                   right.MapNewMemberMappings) &&
               StringComparer.Ordinal.Equals(
                   left.MapNewUnsupportedExceptionMessage,
                   right.MapNewUnsupportedExceptionMessage) &&
               StringComparer.Ordinal.Equals(
                   left.MapExistingUnsupportedExceptionMessage,
                   right.MapExistingUnsupportedExceptionMessage) &&
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
        bool mapNew)
    {
        if (mapping.ControlFlow is { } controlFlow)
        {
            return mapNew
                ? controlFlow.MapNewRoot
                : controlFlow.MapExistingRoot;
        }

        var leaf = mapNew
            ? mapping
            : mapping with
            {
                MapNewFactory = null,
                MapNewConstructor = null
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
