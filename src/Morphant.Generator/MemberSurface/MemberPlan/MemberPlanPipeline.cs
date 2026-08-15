using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    private static DestinationPlanCandidate BuildCandidate(
        CanonicalMappingPairCandidate candidate,
        CSharpCompilation compilation)
    {
        var destination = DestinationCapabilityPolicy
            .GetDestinationType(
                candidate.Pair.DestinationType,
                compilation)
            .OriginalDefinition;
        var assemblyIdentity =
            destination.ContainingAssembly.Identity.ToString();
        var metadataName = SymbolNameHelper.GetFullMetadataName(destination);

        return new DestinationPlanCandidate(
            candidate.CandidateIdentity,
            assemblyIdentity + "|" + metadataName,
            assemblyIdentity,
            metadataName,
            candidate.Pair.Capabilities.StructuredConstruction);
    }

    private static MemberPlanGenerationInput? BuildGenerationInput(
        DestinationPlanCandidate candidate,
        DestinationPlanCoordination coordination)
    {
        return coordination.IsOwner(candidate)
            ? new MemberPlanGenerationInput(
                candidate.AssemblyIdentity,
                candidate.MetadataName,
                candidate.IncludeInitOnlyProperties,
                GeneratedSourceHintName.Create(
                    "Member",
                    HintNameCollisions.Resolve(
                        coordination.HintNameAllocations,
                        candidate.MetadataName)))
            : null;
    }

    private static MemberPlanModelInput? TryBuildModelInput(
        MemberPlanGenerationInput generationInput,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        var destination = TypeContractDependencies.ResolveType(
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
        var model = MemberPlanModelBuilder.Build(
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
        string HintName);

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
            return left.GenerationInput == right.GenerationInput &&
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
            var hash = value.GenerationInput.GetHashCode();

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
