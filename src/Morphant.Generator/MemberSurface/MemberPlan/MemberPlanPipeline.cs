using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.Incrementality;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.MemberSurface.MemberPlan;

internal static class MemberPlanPipeline
{
    public static IncrementalValuesProvider<MemberPlanModelResult>
        BuildModels(
            IncrementalValueProvider<CompilationContext> compilationContext,
            IncrementalValuesProvider<CanonicalMappingPairCandidate>
                canonicalPairs)
    {
        var candidates = canonicalPairs
            .Where(static candidate => candidate.Pair.Capabilities.Members)
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
            .WithComparer(MemberPlanModelInputComparer.Instance);

        return modelInputs
            .Select(static (input, cancellationToken) =>
                BuildModel(input, cancellationToken))
            .WithComparer(MemberPlanModelResultComparer.Instance)
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildMemberPlanModels);
    }

    private static MemberPlanCandidate BuildCandidate(
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

        return new MemberPlanCandidate(
            new DestinationPlanCandidate(
                candidate.CandidateIdentity,
                tuple is null
                    ? assemblyIdentity + "|" + metadataName
                    : "tuple|" + planIdentity,
                assemblyIdentity,
                metadataName,
                candidate.Pair.Capabilities.StructuredConstruction),
            definition,
            tuple is not null,
            planIdentity);
    }

    private static MemberPlanGenerationInput? BuildGenerationInput(
        MemberPlanCandidate candidate,
        DestinationPlanCoordination coordination)
    {
        return coordination.IsOwner(candidate.Coordination)
            ? new MemberPlanGenerationInput(
                candidate.Coordination.AssemblyIdentity,
                candidate.Coordination.MetadataName,
                candidate.Coordination.IncludeInitOnlyProperties,
                GeneratedSourceHintName.Create(
                    "Member",
                    HintNameCollisions.Resolve(
                        coordination.HintNameAllocations,
                        candidate.Coordination.MetadataName)),
                candidate.Destination,
                candidate.IsTuple,
                candidate.PlanIdentity)
            : null;
    }

    private static MemberPlanModelInput? TryBuildModelInput(
        MemberPlanGenerationInput generationInput,
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

        return new MemberPlanModelInput(
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

    private static MemberPlanModelResult BuildModel(
        MemberPlanModelInput input,
        CancellationToken cancellationToken)
    {
        var model = input.GenerationInput.IsTuple &&
                    BclTupleShapePolicy.TryCreate(input.Destination) is
                        { } tuple
            ? BclTuplePlanModelBuilder.BuildMembers(
                tuple,
                input.Compilation)
            : MemberPlanModelBuilder.Build(
                input.Destination,
                input.GenerationInput.IncludeInitOnlyProperties,
                input.Compilation,
                cancellationToken);

        return new MemberPlanModelResult(
            input.GenerationInput.HintName,
            model);
    }

    private readonly record struct MemberPlanGenerationInput(
        string AssemblyIdentity,
        string MetadataName,
        bool IncludeInitOnlyProperties,
        string HintName,
        INamedTypeSymbol Destination,
        bool IsTuple,
        string PlanIdentity);

    private readonly record struct MemberPlanCandidate(
        DestinationPlanCandidate Coordination,
        INamedTypeSymbol Destination,
        bool IsTuple,
        string PlanIdentity);

    private readonly record struct MemberPlanModelInput(
        MemberPlanGenerationInput GenerationInput,
        INamedTypeSymbol Destination,
        CSharpCompilation Compilation,
        ImmutableArray<TypeContractDependency> Dependencies,
        LanguageVersion LanguageVersion,
        string CompilationAssemblyIdentity,
        NullableContextOptions NullableContextOptions,
        MetadataImportOptions MetadataImportOptions);

    private sealed class MemberPlanModelInputComparer :
        IEqualityComparer<MemberPlanModelInput>
    {
        public static MemberPlanModelInputComparer Instance { get; } =
            new();

        public bool Equals(
            MemberPlanModelInput left,
            MemberPlanModelInput right)
        {
            return StringComparer.Ordinal.Equals(
                       left.GenerationInput.AssemblyIdentity,
                       right.GenerationInput.AssemblyIdentity) &&
                   StringComparer.Ordinal.Equals(
                       left.GenerationInput.MetadataName,
                       right.GenerationInput.MetadataName) &&
                   left.GenerationInput.IncludeInitOnlyProperties ==
                       right.GenerationInput.IncludeInitOnlyProperties &&
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

        public int GetHashCode(MemberPlanModelInput value)
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
        return StringComparer.Ordinal.Equals(
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
