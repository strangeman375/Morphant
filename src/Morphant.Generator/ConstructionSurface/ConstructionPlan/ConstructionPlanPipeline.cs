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
            IncrementalGeneratorInitializationContext context,
            IncrementalValuesProvider<CanonicalMappingPairCandidate>
                canonicalPairs)
    {
        var candidates = GeneratorStageGuard.Select(
            context,
            canonicalPairs.Where(static candidate =>
                candidate.Pair.Capabilities.StructuredConstruction),
            "BuildConstructionPlanCandidates",
            static (candidate, _) => BuildCandidate(candidate),
            static candidate =>
                candidate.Pair.Registration.Syntax.GetLocation());
        var coordinationInputs = candidates
            .Select(static (candidate, _) => candidate.Coordination)
            .Collect();
        var coordination = GeneratorStageGuard.Select(
                context,
                coordinationInputs,
                "CoordinateConstructionPlans",
                static (values, cancellationToken) =>
                    DestinationPlanCoordinationBuilder.Build(
                        values,
                        cancellationToken),
                EmptyCoordination())
            .WithComparer(DestinationPlanCoordinationComparer.Instance);
        var generationInputs = GeneratorStageGuard
            .Select(
                context,
                candidates.Combine(coordination),
                "BuildConstructionPlanGenerationInputs",
                static (source, _) =>
                    BuildGenerationInput(source.Left, source.Right),
                static _ => Location.None)
            .WhereHasValue();
        var modelInputs = GeneratorStageGuard
            .Select(
                context,
                generationInputs,
                "BuildConstructionPlanModelInputs",
                static (generationInput, cancellationToken) =>
                    TryBuildModelInput(
                        generationInput,
                        cancellationToken),
                static _ => Location.None)
            .WhereHasValue()
            .WithComparer(ConstructionPlanModelInputComparer.Instance);

        return GeneratorStageGuard
            .Select(
                context,
                modelInputs,
                MorphantGeneratorStageNames.BuildConstructionPlanModels,
                static (input, cancellationToken) =>
                    BuildModel(input, cancellationToken),
                static _ => Location.None)
            .WithComparer(ConstructionPlanModelResultComparer.Instance)
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildConstructionPlanModels);
    }

    private static DestinationPlanCoordination EmptyCoordination()
    {
        return new DestinationPlanCoordination(
            ImmutableArray<DestinationPlanOwner>.Empty,
            new HintNameAllocations(
                ImmutableArray<HintNameAllocation>.Empty));
    }

    private static ConstructionPlanCandidate BuildCandidate(
        CanonicalMappingPairCandidate candidate)
    {
        var compilation = candidate.Compilation;
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
        var hintStableIdentity = metadataName;
        var readableHintNamePart = tuple is null
            ? HintNameHelper.ToHintNamePart(metadataName)
            : HintNameHelper.ToHintNamePart(
                "Tuple." + BclTuplePlanNaming.BuildHintIdentity(tuple));

        return new ConstructionPlanCandidate(
            new DestinationPlanCandidate(
                candidate.CandidateIdentity,
                tuple is null
                    ? assemblyIdentity + "|" + metadataName
                    : "tuple|" + planIdentity,
                assemblyIdentity,
                metadataName,
                hintStableIdentity,
                readableHintNamePart,
                IncludeInitOnlyProperties: false),
            definition,
            tuple is not null,
            planIdentity,
            compilation,
            ((CSharpParseOptions)candidate.Pair.Registration.Syntax
                .SyntaxTree.Options).LanguageVersion);
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
                        candidate.Coordination.HintStableIdentity,
                        candidate.Coordination.ReadableHintNamePart)),
                candidate.Destination,
                candidate.IsTuple,
                candidate.PlanIdentity,
                candidate.Compilation,
                candidate.LanguageVersion)
            : null;
    }

    private static ConstructionPlanModelInput? TryBuildModelInput(
        ConstructionPlanGenerationInput generationInput,
        CancellationToken cancellationToken)
    {
        var compilation = generationInput.Compilation;
        var destination = generationInput.IsTuple
            ? generationInput.Destination
            : TypeContractDependencies.ResolveType(
                compilation,
                generationInput.AssemblyIdentity,
                generationInput.MetadataName);

        if (destination is null)
        {
            return null;
        }

        return new ConstructionPlanModelInput(
            generationInput,
            destination,
            compilation,
            TypeContractDependencies.Build(
                destination,
                compilation,
                cancellationToken),
            generationInput.LanguageVersion,
            compilation.Assembly.Identity.ToString(),
            compilation.Options.NullableContextOptions,
            compilation.Options.MetadataImportOptions);
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
                GeneratedPlanNaming.BuildNamespace(destination, input.Compilation),
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
        string PlanIdentity,
        CSharpCompilation Compilation,
        LanguageVersion LanguageVersion);

    private readonly record struct ConstructionPlanCandidate(
        DestinationPlanCandidate Coordination,
        INamedTypeSymbol Destination,
        bool IsTuple,
        string PlanIdentity,
        CSharpCompilation Compilation,
        LanguageVersion LanguageVersion);

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
