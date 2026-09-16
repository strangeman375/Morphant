using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.Incrementality;

internal static class DestinationPlanPipeline
{
    public static IncrementalValuesProvider<DestinationPlanModelInput> BuildInputs(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<CanonicalMappingPairCandidate> canonicalPairs,
        DestinationPlanKind kind)
    {
        var stage = kind.ToString();
        var candidates = GeneratorStageGuard.Select(
            context,
            canonicalPairs.Where(candidate => Supports(candidate.Pair, kind)),
            "Build" + stage + "PlanCandidates",
            (candidate, _) => BuildCandidate(candidate, kind),
            static candidate => candidate.Pair.Registration.Syntax.GetLocation());
        var coordination = GeneratorStageGuard.Select(
                context,
                candidates.Select(static (candidate, _) => candidate.Coordination).Collect(),
                "Coordinate" + stage + "Plans",
                static (values, cancellationToken) =>
                    DestinationPlanCoordinationBuilder.Build(values, cancellationToken),
                new DestinationPlanCoordination(ImmutableArray<DestinationPlanOwner>.Empty))
            .WithComparer(DestinationPlanCoordinationComparer.Instance);

        // Discard coordination-only changes before walking type dependencies.
        // The selected candidate itself is already a stable incremental value.
        var owners = candidates.Combine(coordination)
            .Select(static (source, _) => source.Right.IsOwner(source.Left.Coordination)
                ? source.Left
                : (DestinationPlanGenerationCandidate?)null)
            .WhereHasValue();

        return GeneratorStageGuard.Select(
                context,
                owners,
                "Build" + stage + "PlanModelInputs",
                (candidate, cancellationToken) =>
                    TryBuildInput(candidate, kind, cancellationToken),
                static _ => Location.None)
            .WhereHasValue()
            .WithComparer(DestinationPlanModelInputComparer.Instance)
            .WithTrackingName("Build" + stage + "PlanModelInputs");
    }

    // Configuration binding needs one local declaration per destination. Global
    // output ownership remains coordinated separately by the incremental path.
    public static IEnumerable<DestinationPlanDefinition> BuildDefinitions(
        ImmutableArray<CanonicalMappingPairCandidate> candidates,
        Compilation compilation,
        DestinationPlanKind kind,
        CancellationToken cancellationToken)
    {
        var definitions = new Dictionary<string, DestinationPlanDefinition>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Supports(candidate.Pair, kind)) continue;

            var target = DestinationPlanTarget.Create(candidate.Pair.DestinationType, compilation);
            if (!definitions.ContainsKey(target.Identity))
            {
                definitions.Add(target.Identity, new DestinationPlanDefinition(
                    target.Destination,
                    kind == DestinationPlanKind.Member && candidate.Pair.Capabilities.StructuredConstruction));
            }
        }

        return definitions.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => pair.Value);
    }

    private static bool Supports(MappingPairModel pair, DestinationPlanKind kind) =>
        kind == DestinationPlanKind.Construction
            ? pair.Capabilities.StructuredConstruction
            : pair.Capabilities.Members;

    private static DestinationPlanGenerationCandidate BuildCandidate(
        CanonicalMappingPairCandidate candidate,
        DestinationPlanKind kind)
    {
        var target = DestinationPlanTarget.Create(candidate.Pair.DestinationType, candidate.Compilation);
        return new DestinationPlanGenerationCandidate(
            new DestinationPlanCandidate(
                candidate.CandidateIdentity,
                target.Identity,
                target.AssemblyIdentity,
                target.MetadataName,
                kind == DestinationPlanKind.Member && candidate.Pair.Capabilities.StructuredConstruction),
            target,
            candidate.Compilation,
            ((CSharpParseOptions)candidate.Pair.Registration.Syntax.SyntaxTree.Options).LanguageVersion);
    }

    private static DestinationPlanModelInput? TryBuildInput(
        DestinationPlanGenerationCandidate candidate,
        DestinationPlanKind kind,
        CancellationToken cancellationToken)
    {
        var target = candidate.Target;
        var compilation = candidate.Compilation;
        var destination = target.IsTuple
            ? target.Destination
            : TypeContractDependencies.ResolveType(compilation, target.AssemblyIdentity, target.MetadataName);
        if (destination is null) return null;

        return new DestinationPlanModelInput(
            target.AssemblyIdentity,
            target.MetadataName,
            candidate.Coordination.IncludeInitOnlyProperties,
            GeneratedSourceHintName.ForDestination(kind.ToString(), target.Destination, compilation),
            destination,
            target.IsTuple,
            target.PlanIdentity,
            compilation,
            TypeContractDependencies.Build(destination, compilation, cancellationToken),
            candidate.LanguageVersion,
            compilation.Assembly.Identity.ToString(),
            compilation.Options.NullableContextOptions,
            compilation.Options.MetadataImportOptions);
    }

    private readonly record struct DestinationPlanGenerationCandidate(
        DestinationPlanCandidate Coordination,
        DestinationPlanTarget Target,
        CSharpCompilation Compilation,
        LanguageVersion LanguageVersion);

    private sealed class DestinationPlanModelInputComparer : IEqualityComparer<DestinationPlanModelInput>
    {
        public static DestinationPlanModelInputComparer Instance { get; } = new();

        public bool Equals(DestinationPlanModelInput left, DestinationPlanModelInput right) =>
            StringComparer.Ordinal.Equals(left.AssemblyIdentity, right.AssemblyIdentity) &&
            StringComparer.Ordinal.Equals(left.MetadataName, right.MetadataName) &&
            left.IncludeInitOnlyProperties == right.IncludeInitOnlyProperties &&
            StringComparer.Ordinal.Equals(left.HintName, right.HintName) &&
            left.IsTuple == right.IsTuple &&
            StringComparer.Ordinal.Equals(left.PlanIdentity, right.PlanIdentity) &&
            left.LanguageVersion == right.LanguageVersion &&
            StringComparer.Ordinal.Equals(left.CompilationAssemblyIdentity, right.CompilationAssemblyIdentity) &&
            left.NullableContextOptions == right.NullableContextOptions &&
            left.MetadataImportOptions == right.MetadataImportOptions &&
            TypeContractDependencies.Equal(left.Dependencies, right.Dependencies);

        public int GetHashCode(DestinationPlanModelInput value)
        {
            var hash = StringComparer.Ordinal.GetHashCode(value.HintName);
            hash = TypeContractDependencies.AddHash(hash, value.PlanIdentity);
            hash = TypeContractDependencies.AddHash(hash, value.IsTuple);
            hash = TypeContractDependencies.AddHash(hash, value.IncludeInitOnlyProperties);
            hash = TypeContractDependencies.AddHash(hash, value.LanguageVersion);
            hash = TypeContractDependencies.AddHash(hash, value.CompilationAssemblyIdentity);
            hash = TypeContractDependencies.AddHash(hash, value.NullableContextOptions);
            hash = TypeContractDependencies.AddHash(hash, value.MetadataImportOptions);
            return TypeContractDependencies.AddHash(hash, value.Dependencies);
        }
    }
}

internal enum DestinationPlanKind
{
    Construction,
    Member
}

internal readonly record struct DestinationPlanDefinition(
    INamedTypeSymbol Destination,
    bool IncludeInitOnlyProperties);

internal readonly record struct DestinationPlanModelInput(
    string AssemblyIdentity,
    string MetadataName,
    bool IncludeInitOnlyProperties,
    string HintName,
    INamedTypeSymbol Destination,
    bool IsTuple,
    string PlanIdentity,
    CSharpCompilation Compilation,
    ImmutableArray<TypeContractDependency> Dependencies,
    LanguageVersion LanguageVersion,
    string CompilationAssemblyIdentity,
    NullableContextOptions NullableContextOptions,
    MetadataImportOptions MetadataImportOptions);
