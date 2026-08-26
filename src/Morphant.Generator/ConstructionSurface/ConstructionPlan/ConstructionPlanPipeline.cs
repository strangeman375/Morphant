using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.Incrementality;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.ConstructionSurface.ConstructionPlan;

internal static class ConstructionPlanPipeline
{
    public static IncrementalValuesProvider<ConstructionPlanModelResult>
        BuildModels(
            IncrementalValueProvider<CompilationContext> compilationContext,
            IncrementalValuesProvider<CanonicalMappingPairCandidate>
                canonicalPairs)
    {
        var candidates = canonicalPairs
            .Where(static candidate =>
                candidate.Pair.Capabilities.StructuredConstruction)
            .Combine(compilationContext)
            .Select(static (source, _) =>
                BuildCandidate(
                    source.Left,
                    source.Right.Compilation));
        var coordination = candidates
            .Select(static (candidate, _) => candidate.Coordination)
            .Collect()
            .Select(static (values, cancellationToken) =>
                DestinationPlanCoordinationBuilder.Build(
                    values,
                    cancellationToken))
            .WithComparer(DestinationPlanCoordinationComparer.Instance);
        var generationInputs = candidates
            .Combine(coordination)
            .Select(static (source, _) =>
                BuildGenerationInput(source.Left, source.Right))
            .WhereHasValue();
        var modelInputs = generationInputs
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                TryBuildModelInput(
                    source.Left,
                    source.Right,
                    cancellationToken))
            .WhereHasValue()
            .WithComparer(ConstructionPlanModelInputComparer.Instance);

        return modelInputs
            .Select(static (input, cancellationToken) =>
                BuildModel(input, cancellationToken))
            .WithComparer(ConstructionPlanModelResultComparer.Instance)
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildConstructionPlanModels);
    }

    private static ConstructionPlanCandidate BuildCandidate(
        CanonicalMappingPairCandidate candidate,
        CSharpCompilation compilation)
    {
        var destination = DestinationCapabilityPolicy
            .GetDestinationType(
                candidate.Pair.DestinationType,
                compilation);
        var tuple = BclTupleShapePolicy.TryCreate(destination);
        var planIdentity = tuple is null
            ? string.Empty
            : BclTuplePlanNaming.BuildStableIdentity(tuple);
        var definition = tuple is null
            ? destination.OriginalDefinition
            : destination;
        var assemblyIdentity =
            definition.ContainingAssembly.Identity.ToString();
        var metadataName = tuple is null
            ? SymbolNameHelper.GetFullMetadataName(definition)
            : "Tuple." + planIdentity;

        return new ConstructionPlanCandidate(
            new DestinationPlanCandidate(
                candidate.CandidateIdentity,
                tuple is null
                    ? assemblyIdentity + "|" + metadataName
                    : "tuple|" + planIdentity,
                assemblyIdentity,
                metadataName,
                IncludeInitOnlyProperties: false),
            definition,
            tuple is not null,
            planIdentity);
    }

    private static ConstructionPlanGenerationInput? BuildGenerationInput(
        ConstructionPlanCandidate candidate,
        DestinationPlanCoordination coordination)
    {
        return coordination.IsOwner(candidate.Coordination)
            ? new ConstructionPlanGenerationInput(
                candidate.Coordination.AssemblyIdentity,
                candidate.Coordination.MetadataName,
                GeneratedSourceHintName.Create(
                    "Construction",
                    HintNameCollisions.Resolve(
                        coordination.HintNameAllocations,
                        candidate.Coordination.MetadataName)),
                candidate.Destination,
                candidate.IsTuple,
                candidate.PlanIdentity)
            : null;
    }

    private static ConstructionPlanModelInput? TryBuildModelInput(
        ConstructionPlanGenerationInput generationInput,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        var destination = generationInput.IsTuple
            ? generationInput.Destination
            : TypeContractDependencies.ResolveType(
                context.Compilation,
                generationInput.AssemblyIdentity,
                generationInput.MetadataName);

        if (destination is null)
        {
            return null;
        }

        return new ConstructionPlanModelInput(
            generationInput,
            destination,
            context.Compilation,
            TypeContractDependencies.Build(
                destination,
                context.Compilation,
                cancellationToken),
            context.LanguageVersion,
            context.Compilation.Assembly.Identity.ToString(),
            context.Compilation.Options.NullableContextOptions,
            context.Compilation.Options.MetadataImportOptions);
    }

    private static ConstructionPlanModelResult BuildModel(
        ConstructionPlanModelInput input,
        CancellationToken cancellationToken)
    {
        var destination = input.Destination.OriginalDefinition;
        var model = input.GenerationInput.IsTuple &&
                    BclTupleShapePolicy.TryCreate(input.Destination) is
                        { } tuple
            ? BclTuplePlanModelBuilder.BuildConstruction(
                tuple,
                input.Compilation)
            : ConstructionPlanModelBuilder.Build(
                destination,
                GeneratedPlanNaming.BuildNamespace(destination),
                GeneratedPlanNaming.BuildConstructionTypeName(destination),
                input.Compilation,
                cancellationToken);

        return new ConstructionPlanModelResult(
            input.GenerationInput.HintName,
            model);
    }

    private readonly record struct ConstructionPlanGenerationInput(
        string AssemblyIdentity,
        string MetadataName,
        string HintName,
        INamedTypeSymbol Destination,
        bool IsTuple,
        string PlanIdentity);

    private readonly record struct ConstructionPlanCandidate(
        DestinationPlanCandidate Coordination,
        INamedTypeSymbol Destination,
        bool IsTuple,
        string PlanIdentity);

    private readonly record struct ConstructionPlanModelInput(
        ConstructionPlanGenerationInput GenerationInput,
        INamedTypeSymbol Destination,
        CSharpCompilation Compilation,
        ImmutableArray<TypeContractDependency> Dependencies,
        LanguageVersion LanguageVersion,
        string CompilationAssemblyIdentity,
        NullableContextOptions NullableContextOptions,
        MetadataImportOptions MetadataImportOptions);

    private sealed class ConstructionPlanModelInputComparer :
        IEqualityComparer<ConstructionPlanModelInput>
    {
        public static ConstructionPlanModelInputComparer Instance { get; } =
            new();

        public bool Equals(
            ConstructionPlanModelInput left,
            ConstructionPlanModelInput right)
        {
            return StringComparer.Ordinal.Equals(
                       left.GenerationInput.AssemblyIdentity,
                       right.GenerationInput.AssemblyIdentity) &&
                   StringComparer.Ordinal.Equals(
                       left.GenerationInput.MetadataName,
                       right.GenerationInput.MetadataName) &&
                   StringComparer.Ordinal.Equals(
                       left.GenerationInput.HintName,
                       right.GenerationInput.HintName) &&
                   left.GenerationInput.IsTuple ==
                       right.GenerationInput.IsTuple &&
                   StringComparer.Ordinal.Equals(
                       left.GenerationInput.PlanIdentity,
                       right.GenerationInput.PlanIdentity) &&
                   left.LanguageVersion == right.LanguageVersion &&
                   StringComparer.Ordinal.Equals(
                       left.CompilationAssemblyIdentity,
                       right.CompilationAssemblyIdentity) &&
                   left.NullableContextOptions ==
                       right.NullableContextOptions &&
                   left.MetadataImportOptions ==
                       right.MetadataImportOptions &&
                   TypeContractDependencies.Equal(
                       left.Dependencies,
                       right.Dependencies);
        }

        public int GetHashCode(ConstructionPlanModelInput value)
        {
            var hash = StringComparer.Ordinal.GetHashCode(
                value.GenerationInput.HintName);

            hash = TypeContractDependencies.AddHash(
                hash,
                value.GenerationInput.PlanIdentity);
            hash = TypeContractDependencies.AddHash(
                hash,
                value.GenerationInput.IsTuple);

            hash = TypeContractDependencies.AddHash(
                hash,
                value.LanguageVersion);
            hash = TypeContractDependencies.AddHash(
                hash,
                value.CompilationAssemblyIdentity);
            hash = TypeContractDependencies.AddHash(
                hash,
                value.NullableContextOptions);
            hash = TypeContractDependencies.AddHash(
                hash,
                value.MetadataImportOptions);

            return TypeContractDependencies.AddHash(
                hash,
                value.Dependencies);
        }
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
        return StringComparer.Ordinal.Equals(
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
