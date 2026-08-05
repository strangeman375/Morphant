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
                out var resultParameter) ||
            !TryGetAssignments(
                lambda,
                out var assignments))
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

            if (configured.Expression.SemanticModel.GetSymbolInfo(
                    assignment.Left,
                    cancellationToken).Symbol is not
                    IPropertySymbol planMember ||
                !occupiedNames.Add(planMember.Name) ||
                !writableMembersByName.TryGetValue(
                    planMember.Name,
                    out var destinationMember))
            {
                return BasicMembersMappingResult.Unsupported(
                    UnsupportedMembersMessage);
            }

            if (DeclarativeMemberMarker.TryGetKind(
                    assignment.Right,
                    configured.Expression.SemanticModel,
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
                    return BasicMembersMappingResult.Unsupported(
                        AutomaticMemberUnavailableMessage);
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
                    assignment.Right,
                    destinationMember,
                    mapping,
                    configured.Expression.SemanticModel,
                    mapperType,
                    sourceParameter,
                    previousParameter,
                    resultParameter,
                    lambda,
                    cancellationToken,
                    out var explicitPlan))
            {
                return BasicMembersMappingResult.Unsupported(
                    UnsupportedMembersMessage);
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
                convention.MapNew.Where(mapping =>
                    !occupiedNames.Contains(
                        mapping.DestinationMemberName)));
            mapNewPost.AddRange(
                convention.MapNewPost.Where(mapping =>
                    !occupiedNames.Contains(
                        mapping.DestinationMemberName)));
            mapReplacement.AddRange(
                convention.MapReplacement.Where(mapping =>
                    !occupiedNames.Contains(
                        mapping.DestinationMemberName)));
            mapReplacementPost.AddRange(
                convention.MapReplacementPost.Where(mapping =>
                    !occupiedNames.Contains(
                        mapping.DestinationMemberName)));
            mapExisting.AddRange(
                convention.MapExisting.Where(mapping =>
                    !occupiedNames.Contains(
                        mapping.DestinationMemberName)));
        }

        var immutableMapNew = mapNew.ToImmutable();

        return new BasicMembersMappingResult(
            new ConventionMemberMappingPlan(
                immutableMapNew,
                mapNewPost.ToImmutable(),
                mapReplacement.ToImmutable(),
                mapReplacementPost.ToImmutable(),
                mapExisting.ToImmutable(),
                ConventionMemberMappingPlanner
                    .HasUnmappedRequiredMembers(
                        destination,
                        immutableMapNew,
                        cancellationToken),
                hasExplicitCreationOnlyMappings,
                hasResultDependentCreationOnlyMappings),
            UnsupportedMessage: null);
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
    string? UnsupportedMessage)
{
    public static BasicMembersMappingResult Unsupported(string message) =>
        new(default, message);
}

internal readonly record struct ExplicitMemberMappingPlan(
    TypeMapperMemberMappingModel? MapNew,
    TypeMapperMemberMappingModel? MapNewPost,
    TypeMapperMemberMappingModel? MapReplacement,
    TypeMapperMemberMappingModel? MapReplacementPost,
    TypeMapperMemberMappingModel? MapExisting,
    bool IsCreationOnly,
    bool IsResultDependent);
