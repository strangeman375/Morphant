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
        "The configured Members plan is not supported yet.";

    private const string AutomaticMemberUnavailableMessage =
        "A configured Auto member cannot be mapped by convention.";

    public static BasicMembersMappingResult Build(
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

            var emptyMapNew =
                ImmutableArray<TypeMapperMemberMappingModel>.Empty;

            return new BasicMembersMappingResult(
                new ConventionMemberMappingPlan(
                    emptyMapNew,
                    [],
                    emptyMapNew,
                    [],
                    [],
                    ConventionMemberMappingPlanner
                        .HasUnmappedRequiredMembers(
                            destination,
                            emptyMapNew,
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
        var conventionMapNewByName = convention.MapNew.ToDictionary(
            static member => member.DestinationMemberName,
            StringComparer.Ordinal);
        var conventionMapExistingByName =
            convention.MapExisting.ToDictionary(
                static member => member.DestinationMemberName,
                StringComparer.Ordinal);
        var conventionMapNewPostByName =
            convention.MapNewPost.ToDictionary(
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
                    conventionMapNewByName,
                    conventionMapNewPostByName,
                    conventionMapExistingByName,
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
            conventionMapNewByName,
        IReadOnlyDictionary<string, TypeMapperMemberMappingModel>
            conventionMapNewPostByName,
        IReadOnlyDictionary<string, TypeMapperMemberMappingModel>
            conventionMapExistingByName,
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
        var mapNew =
            ImmutableArray.CreateBuilder<TypeMapperMemberMappingModel>();
        var mapNewPost =
            ImmutableArray.CreateBuilder<TypeMapperMemberMappingModel>();
        var mapReplacement =
            ImmutableArray.CreateBuilder<TypeMapperMemberMappingModel>();
        var mapReplacementPost =
            ImmutableArray.CreateBuilder<TypeMapperMemberMappingModel>();
        var mapExisting =
            ImmutableArray.CreateBuilder<TypeMapperMemberMappingModel>();
        var occupiedNames = new HashSet<string>(StringComparer.Ordinal);
        var hasExplicitCreationOnlyMappings = false;
        var hasResultDependentCreationOnlyMappings = false;

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

                if (!conventionMapNewByName.TryGetValue(
                        destinationMember.Name,
                        out var automaticMapNew))
                {
                    plan = default;
                    unsupportedMessage =
                        AutomaticMemberUnavailableMessage;
                    return false;
                }

                mapNew.Add(automaticMapNew);
                mapReplacement.Add(automaticMapNew);

                if (conventionMapNewPostByName.TryGetValue(
                        destinationMember.Name,
                        out var automaticMapNewPost))
                {
                    mapNewPost.Add(automaticMapNewPost);
                    mapReplacementPost.Add(automaticMapNewPost);
                }

                if (conventionMapExistingByName.TryGetValue(
                        destinationMember.Name,
                        out var automaticMapExisting))
                {
                    mapExisting.Add(automaticMapExisting);
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
                    cancellationToken,
                    out var explicitPlan))
            {
                plan = default;
                unsupportedMessage = UnsupportedMembersMessage;
                return false;
            }

            if (explicitPlan.MapNew is { } explicitMapNew)
            {
                mapNew.Add(explicitMapNew);
            }

            if (explicitPlan.MapNewPost is { } createPost)
            {
                mapNewPost.Add(createPost);
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

            if (explicitPlan.MapExisting is { } existing)
            {
                mapExisting.Add(existing);
            }

            hasExplicitCreationOnlyMappings |=
                explicitPlan.IsCreationOnly;
            hasResultDependentCreationOnlyMappings |=
                explicitPlan.IsCreationOnly &&
                explicitPlan.IsResultDependent;
        }

        if (memberSelection == MemberSelectionValue.Auto)
        {
            mapNew.AddRange(
                convention.MapNew.Where(candidate =>
                    !occupiedNames.Contains(
                        candidate.DestinationMemberName)));
            mapNewPost.AddRange(
                convention.MapNewPost.Where(candidate =>
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
            mapExisting.AddRange(
                convention.MapExisting.Where(candidate =>
                    !occupiedNames.Contains(
                        candidate.DestinationMemberName)));
        }

        var immutableMapNew = mapNew.ToImmutable();

        plan = new ConventionMemberMappingPlan(
            immutableMapNew,
            mapNewPost.ToImmutable(),
            mapReplacement.ToImmutable(),
            mapReplacementPost.ToImmutable(),
            mapExisting.ToImmutable(),
            ConventionMemberMappingPlanner.HasUnmappedRequiredMembers(
                destination,
                immutableMapNew,
                cancellationToken),
            hasExplicitCreationOnlyMappings,
            hasResultDependentCreationOnlyMappings);
        unsupportedMessage = null;
        return true;
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
        CancellationToken cancellationToken,
        out ExplicitMemberMappingPlan plan)
    {
        if (!ConstructExpressionRewriter.TryRewrite(
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
                cancellationToken,
                out var mapNewExpression) ||
            !ConstructExpressionRewriter.TryRewrite(
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
                cancellationToken,
                out var mapReplacementExpression) ||
            !ConstructExpressionRewriter.TryRewrite(
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
                cancellationToken,
                out var mapExistingExpression))
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
        TypeMapperMemberMappingModel BuildMapping(string valueExpression) =>
            new(
                SourceMemberName: string.Empty,
                destinationMember.Name,
                destinationMember.IsRequired,
                SourceValueLocalName: null,
                ExplicitValueExpression: valueExpression,
                ExplicitValueTypeName: valueTypeName,
                IsResultDependent: isResultDependent);

        var mapNew = BuildMapping(mapNewExpression);
        var mapReplacement = BuildMapping(mapReplacementExpression);
        var mapExisting = BuildMapping(mapExistingExpression);

        plan = new ExplicitMemberMappingPlan(
            MapNew: isResultDependent ? null : mapNew,
            MapNewPost: destinationMember.CanAssign
                ? mapNew
                : null,
            MapReplacement: isResultDependent
                ? null
                : mapReplacement,
            MapReplacementPost: destinationMember.CanAssign
                ? mapReplacement
                : null,
            MapExisting: destinationMember.CanAssign
                ? mapExisting
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
    TypeMapperMemberMappingModel? MapNew,
    TypeMapperMemberMappingModel? MapNewPost,
    TypeMapperMemberMappingModel? MapReplacement,
    TypeMapperMemberMappingModel? MapReplacementPost,
    TypeMapperMemberMappingModel? MapExisting,
    bool IsCreationOnly,
    bool IsResultDependent);
