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
                    [],
                    ConventionMemberMappingPlanner
                        .HasUnmappedRequiredMembers(
                            destination,
                            emptyMapNew,
                            cancellationToken)),
                UnsupportedMessage: null);
        }

        var configured = configuration.Value;

        if (configured.Form !=
                MembersConfigurationForm.SourceAndPrevious ||
            configured.Expression.Syntax is not
                LambdaExpressionSyntax lambda ||
            !TryGetLambdaParameters(
                lambda,
                configured.Expression.SemanticModel,
                cancellationToken,
                out var sourceParameter,
                out var previousParameter) ||
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
        var mapExisting =
            ImmutableArray.CreateBuilder<TypeMapperMemberMappingModel>();
        var occupiedNames = new HashSet<string>(StringComparer.Ordinal);

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

                if (conventionMapNewPostByName.TryGetValue(
                        destinationMember.Name,
                        out var automaticMapNewPost))
                {
                    mapNewPost.Add(automaticMapNewPost);
                }

                if (conventionMapExistingByName.TryGetValue(
                        destinationMember.Name,
                        out var automaticMapExisting))
                {
                    mapExisting.Add(automaticMapExisting);
                }

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
                    lambda,
                    cancellationToken,
                    out var explicitMapNew,
                    out var explicitMapNewPost,
                    out var explicitMapExisting))
            {
                return BasicMembersMappingResult.Unsupported(
                    UnsupportedMembersMessage);
            }

            mapNew.Add(explicitMapNew);

            if (explicitMapNewPost is { } createPost)
            {
                mapNewPost.Add(createPost);
            }

            if (explicitMapExisting is { } existing)
            {
                mapExisting.Add(existing);
            }
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
                mapExisting.ToImmutable(),
                ConventionMemberMappingPlanner
                    .HasUnmappedRequiredMembers(
                        destination,
                        immutableMapNew,
                        cancellationToken)),
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
        LambdaExpressionSyntax transferScope,
        CancellationToken cancellationToken,
        out TypeMapperMemberMappingModel mapNew,
        out TypeMapperMemberMappingModel? mapNewPost,
        out TypeMapperMemberMappingModel? mapExisting)
    {
        if (!ConstructExpressionRewriter.TryRewrite(
                expression,
                semanticModel,
                mapperType,
                sourceParameter,
                mapping.NonNullSourceName,
                previousParameter,
                BuildPreviousSubstitution(mapping, hasPrevious: false),
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
                transferScope,
                cancellationToken,
                out var mapExistingExpression))
        {
            mapNew = default;
            mapNewPost = null;
            mapExisting = null;
            return false;
        }

        var valueTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                destinationMember.Type);
        mapNew = new TypeMapperMemberMappingModel(
            SourceMemberName: string.Empty,
            destinationMember.Name,
            destinationMember.IsRequired,
            SourceValueLocalName: null,
            ExplicitValueExpression: mapNewExpression,
            ExplicitValueTypeName: valueTypeName);
        mapNewPost = destinationMember.CanAssign
            ? mapNew
            : null;
        mapExisting = destinationMember.CanAssign
            ? new TypeMapperMemberMappingModel(
                SourceMemberName: string.Empty,
                destinationMember.Name,
                destinationMember.IsRequired,
                SourceValueLocalName: null,
                ExplicitValueExpression: mapExistingExpression,
                ExplicitValueTypeName: valueTypeName)
            : null;
        return true;
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
        CancellationToken cancellationToken,
        out IParameterSymbol sourceParameter,
        out IParameterSymbol previousParameter)
    {
        if (lambda is not ParenthesizedLambdaExpressionSyntax
            {
                ParameterList.Parameters.Count: 2
            } parenthesized ||
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
            return false;
        }

        sourceParameter = resolvedSource;
        previousParameter = resolvedPrevious;
        return true;
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
