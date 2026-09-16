using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.Incrementality;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.ConstructionSurface.ConstructionPlan;

internal static class ConstructionPlanPipeline
{
    public static IncrementalValuesProvider<ConstructionPlanModelResult> BuildModels(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<CanonicalMappingPairCandidate> canonicalPairs)
    {
        var inputs = DestinationPlanPipeline.BuildInputs(
            context, canonicalPairs, DestinationPlanKind.Construction);
        return GeneratorStageGuard.Select(
                context,
                inputs,
                MorphantGeneratorStageNames.BuildConstructionPlanModels,
                static (input, cancellationToken) => new ConstructionPlanModelResult(
                    input.HintName,
                    BuildModel(input.Destination, input.Compilation, cancellationToken)),
                static _ => Location.None)
            .WithComparer(ConstructionPlanModelResultComparer.Instance)
            .WithTrackingName(MorphantGeneratorStageNames.BuildConstructionPlanModels);
    }

    internal static ImmutableArray<DslSurfaceRequest> BuildRequests(
        ImmutableArray<CanonicalMappingPairCandidate> candidates,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var requests = ImmutableArray.CreateBuilder<DslSurfaceRequest>();
        foreach (var definition in DestinationPlanPipeline.BuildDefinitions(
                     candidates, compilation, DestinationPlanKind.Construction, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var model = BuildModel(
                definition.Destination, compilation, cancellationToken);
            requests.Add(new DslSurfaceRequest(
                GeneratedSourceHintName.ForDestination("Construction", definition.Destination, compilation),
                ConstructionPlanEmitter.Emit(model)));
        }

        return requests.ToImmutable();
    }

    private static ConstructionPlanModel BuildModel(
        INamedTypeSymbol destination,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var tuple = BclTupleShapePolicy.TryCreate(destination);
        return tuple is not null
            ? BclTuplePlanModelBuilder.BuildConstruction(tuple, compilation)
            : ConstructionPlanModelBuilder.Build(
                destination.OriginalDefinition,
                GeneratedPlanNaming.BuildNamespace(destination.OriginalDefinition, compilation),
                GeneratedPlanNaming.BuildConstructionTypeName(destination.OriginalDefinition),
                compilation,
                cancellationToken);
    }
}

internal readonly record struct ConstructionPlanModelResult(
    string HintName,
    ConstructionPlanModel Model);

internal sealed class ConstructionPlanModelResultComparer :
    IEqualityComparer<ConstructionPlanModelResult>
{
    public static ConstructionPlanModelResultComparer Instance { get; } =
        new();

    public bool Equals(
        ConstructionPlanModelResult left,
        ConstructionPlanModelResult right)
    {
        return StringComparer.Ordinal.Equals(
                   left.HintName,
                   right.HintName) &&
               Equal(left.Model, right.Model);
    }

    public int GetHashCode(ConstructionPlanModelResult value)
    {
        return StringComparer.Ordinal.GetHashCode(value.HintName);
    }

    private static bool Equal(
        ConstructionPlanModel left,
        ConstructionPlanModel right)
    {
        return left.ObsoleteWarnings.SequenceEqual(right.ObsoleteWarnings) &&
               StringComparer.Ordinal.Equals(
                   left.Namespace,
                   right.Namespace) &&
               StringComparer.Ordinal.Equals(
                   left.TypeName,
                   right.TypeName) &&
               StringComparer.Ordinal.Equals(
                   left.ConstructorParametersTypeName,
                   right.ConstructorParametersTypeName) &&
               StringComparer.Ordinal.Equals(
                   left.DestinationTypeName,
                   right.DestinationTypeName) &&
               StringComparer.Ordinal.Equals(
                   left.DestinationCref,
                   right.DestinationCref) &&
               StringComparer.Ordinal.Equals(
                   left.ObsoleteAttributeSource,
                   right.ObsoleteAttributeSource) &&
               EqualTypeParameters(
                   left.TypeParameters,
                   right.TypeParameters) &&
               EqualConstructors(
                   left.Constructors,
                   right.Constructors) &&
               left.ConstructorParameterFields.SequenceEqual(
                   right.ConstructorParameterFields);
    }

    private static bool EqualTypeParameters(
        ImmutableArray<ConstructionTypeParameterModel> left,
        ImmutableArray<ConstructionTypeParameterModel> right)
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

    private static bool EqualConstructors(
        ImmutableArray<ConstructionConstructorModel> left,
        ImmutableArray<ConstructionConstructorModel> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!StringComparer.Ordinal.Equals(
                    left[index].ObsoleteAttributeSource,
                    right[index].ObsoleteAttributeSource) ||
                !left[index].Parameters.SequenceEqual(
                    right[index].Parameters))
            {
                return false;
            }
        }

        return true;
    }
}
