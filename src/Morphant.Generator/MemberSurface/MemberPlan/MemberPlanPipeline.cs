using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.Incrementality;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.MemberSurface.MemberPlan;

internal static class MemberPlanPipeline
{
    public static IncrementalValuesProvider<MemberPlanModelResult> BuildModels(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<CanonicalMappingPairCandidate> canonicalPairs)
    {
        var inputs = DestinationPlanPipeline.BuildInputs(
            context, canonicalPairs, DestinationPlanKind.Member);
        return GeneratorStageGuard.Select(
                context,
                inputs,
                MorphantGeneratorStageNames.BuildMemberPlanModels,
                static (input, cancellationToken) => new MemberPlanModelResult(
                    input.HintName,
                    BuildModel(
                        input.Destination, input.IncludeInitOnlyProperties,
                        input.Compilation, cancellationToken)),
                static _ => Location.None)
            .WithComparer(MemberPlanModelResultComparer.Instance)
            .WithTrackingName(MorphantGeneratorStageNames.BuildMemberPlanModels);
    }

    internal static ImmutableArray<DslSurfaceRequest> BuildRequests(
        ImmutableArray<CanonicalMappingPairCandidate> candidates,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var requests = ImmutableArray.CreateBuilder<DslSurfaceRequest>();
        foreach (var definition in DestinationPlanPipeline.BuildDefinitions(
                     candidates, compilation, DestinationPlanKind.Member, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var model = BuildModel(
                definition.Destination, definition.IncludeInitOnlyProperties,
                compilation, cancellationToken);
            requests.Add(new DslSurfaceRequest(
                GeneratedSourceHintName.ForDestination("Member", definition.Destination, compilation),
                MemberPlanEmitter.Emit(model)));
        }

        return requests.ToImmutable();
    }

    private static MemberPlanModel BuildModel(
        INamedTypeSymbol destination,
        bool includeInitOnlyProperties,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var tuple = BclTupleShapePolicy.TryCreate(destination);
        return tuple is not null
            ? BclTuplePlanModelBuilder.BuildMembers(tuple, compilation)
            : MemberPlanModelBuilder.Build(
                destination, includeInitOnlyProperties, compilation, cancellationToken);
    }
}

internal readonly record struct MemberPlanModelResult(
    string HintName,
    MemberPlanModel Model);

internal sealed class MemberPlanModelResultComparer :
    IEqualityComparer<MemberPlanModelResult>
{
    public static MemberPlanModelResultComparer Instance { get; } = new();

    public bool Equals(
        MemberPlanModelResult left,
        MemberPlanModelResult right)
    {
        return StringComparer.Ordinal.Equals(
                   left.HintName,
                   right.HintName) &&
               Equal(left.Model, right.Model);
    }

    public int GetHashCode(MemberPlanModelResult value)
    {
        return StringComparer.Ordinal.GetHashCode(value.HintName);
    }

    private static bool Equal(MemberPlanModel left, MemberPlanModel right)
    {
        return left.ObsoleteWarnings.SequenceEqual(right.ObsoleteWarnings) &&
               StringComparer.Ordinal.Equals(
                   left.Namespace,
                   right.Namespace) &&
               StringComparer.Ordinal.Equals(
                   left.TypeName,
                   right.TypeName) &&
               StringComparer.Ordinal.Equals(
                   left.DestinationCref,
                   right.DestinationCref) &&
               StringComparer.Ordinal.Equals(
                   left.ObsoleteAttributeSource,
                   right.ObsoleteAttributeSource) &&
               EqualTypeParameters(
                   left.TypeParameters,
                   right.TypeParameters) &&
               left.Members.SequenceEqual(right.Members);
    }

    private static bool EqualTypeParameters(
        ImmutableArray<MemberPlanTypeParameterModel> left,
        ImmutableArray<MemberPlanTypeParameterModel> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!StringComparer.Ordinal.Equals(
                    left[index].Name,
                    right[index].Name) ||
                left[index].RequiresNullableAnnotationsDisabled !=
                    right[index].RequiresNullableAnnotationsDisabled ||
                !left[index].Constraints.SequenceEqual(
                    right[index].Constraints,
                    StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
