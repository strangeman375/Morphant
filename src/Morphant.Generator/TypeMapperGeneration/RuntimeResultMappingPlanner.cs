using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class RuntimeResultMappingPlanner
{
    private const string UnsupportedCallbackMessage =
        "The configured runtime result callback cannot be transferred into " +
        "generated code.";

    private const string CreationOnlyMembersMessage =
        "The configured Members plan contains a creation-only rule that " +
        "cannot be applied to a runtime callback result.";

    public static RuntimeResultMappingResult Build(
        ResultPolicyConfigurationModel configuration,
        TypeMapperMappingModel mapping,
        ConventionMemberMappingPlan memberMappings,
        INamedTypeSymbol mapperType,
        HashSet<string> usedGeneratedMethodNames,
        CancellationToken cancellationToken)
    {
        var hasPrevious = configuration.Kind ==
            ResultPolicyKind.ResolveUsing;
        var hasContext = configuration.Form is
            ResultPolicyForm.SourceAndContext or
            ResultPolicyForm.SourcePreviousAndContext;
        var preferredHelperName = hasPrevious
            ? "__ResolveUsing"
            : "__ConstructUsing";
        var helperMethodName = UserResultMappingPlanner.AllocateName(
            preferredHelperName,
            usedGeneratedMethodNames);
        var method = RuntimeCallbackMethodPlanner.Build(
            configuration.Expression,
            hasPrevious,
            hasContext,
            mapperType,
            helperMethodName,
            cancellationToken);

        if (method is null)
        {
            usedGeneratedMethodNames.Remove(helperMethodName);
            return RuntimeResultMappingResult.Unsupported(
                UnsupportedCallbackMessage);
        }

        TypeMapperControlFlowNode BuildCallbackLeaf(bool previousAvailable)
        {
            if (memberMappings.HasExplicitCreationOnlyMappings)
            {
                return BuildUnsupportedLeaf(
                    mapping,
                    create: !previousAvailable,
                    CreationOnlyMembersMessage);
            }

            var postMembers = previousAvailable
                ? memberMappings.MapReplacementPost
                : memberMappings.CreatePost;
            var valueExpression = BuildInvocation(
                method.Value.HelperMethodName,
                mapping,
                hasPrevious,
                hasContext,
                previousAvailable);
            var factory = UserResultMappingPlanner.BuildFactoryMapping(
                mapping,
                postMembers,
                mapperType,
                valueExpression);
            var leaf = mapping with
            {
                CreateDirectExpression = null,
                UpdateDirectExpression = null,
                CreateFactory = factory,
                CreateConstructor = null,
                CreateMemberMappings = [],
                CreatePostMemberMappings = postMembers,
                UpdateMemberMappings = [],
                ControlFlow = null,
                CreateUnsupportedExceptionMessage = null,
                UpdateUnsupportedExceptionMessage = null,
                UnsupportedExceptionMessage = null
            };

            return Leaf(leaf);
        }

        var createRoot = BuildCallbackLeaf(previousAvailable: false);
        var updateRoot = hasPrevious
            ? BuildCallbackLeaf(previousAvailable: true)
            : BuildPreviousLeaf(mapping, memberMappings.Update);

        return new RuntimeResultMappingResult(
            new TypeMapperControlFlowMappingModel(
                createRoot,
                updateRoot),
            [method.Value.HelperMethodDeclaration],
            UnsupportedMessage: null);
    }

    private static string BuildInvocation(
        string helperMethodName,
        TypeMapperMappingModel mapping,
        bool hasPrevious,
        bool hasContext,
        bool previousAvailable)
    {
        var arguments = new List<string>
        {
            mapping.NonNullSourceName
        };

        if (hasPrevious)
        {
            arguments.Add(
                BuildPreviousOptionExpression(
                    mapping,
                    previousAvailable));
        }

        if (hasContext)
        {
            arguments.Add("context");
        }

        return helperMethodName +
               "(" +
               string.Join(", ", arguments) +
               ")";
    }

    private static string BuildPreviousOptionExpression(
        TypeMapperMappingModel mapping,
        bool hasPrevious)
    {
        var optionTypeName =
            "global::Morphant.Option<" +
            mapping.NonNullDestinationTypeName +
            ">";

        return hasPrevious
            ? optionTypeName + ".Some(destination)"
            : optionTypeName + ".None";
    }

    private static TypeMapperControlFlowNode BuildPreviousLeaf(
        TypeMapperMappingModel mapping,
        ImmutableArray<TypeMapperMemberMappingModel> memberMappings)
    {
        return Leaf(
            mapping with
            {
                CreateFactory = null,
                CreateConstructor = null,
                CreateMemberMappings = [],
                CreatePostMemberMappings = [],
                UpdateMemberMappings = memberMappings,
                ControlFlow = null,
                CreateUnsupportedExceptionMessage = null,
                UpdateUnsupportedExceptionMessage = null,
                UnsupportedExceptionMessage = null
            });
    }

    private static TypeMapperControlFlowNode BuildUnsupportedLeaf(
        TypeMapperMappingModel mapping,
        bool create,
        string message)
    {
        return Leaf(
            mapping with
            {
                ControlFlow = null,
                CreateUnsupportedExceptionMessage = create ? message : null,
                UpdateUnsupportedExceptionMessage = create ? null : message,
                UnsupportedExceptionMessage = null
            });
    }

    private static TypeMapperControlFlowNode Leaf(
        TypeMapperMappingModel mapping)
    {
        return new TypeMapperControlFlowNode(
            Locals: [],
            Condition: null,
            WhenTrue: null,
            WhenFalse: null,
            Leaf: mapping,
            ThrowExpression: null);
    }
}

internal readonly record struct RuntimeResultMappingResult(
    TypeMapperControlFlowMappingModel? ControlFlow,
    ImmutableArray<string> HelperMethodDeclarations,
    string? UnsupportedMessage)
{
    public static RuntimeResultMappingResult Unsupported(string message) =>
        new(
            ControlFlow: null,
            HelperMethodDeclarations: [],
            UnsupportedMessage: message);
}
