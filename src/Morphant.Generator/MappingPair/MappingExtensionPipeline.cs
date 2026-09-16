using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.ConstructionSurface.PairConfiguration;
using Morphant.Generator.MemberSurface.PairConfiguration;

namespace Morphant.Generator.MappingPair;

internal static class MappingExtensionPipeline
{
    public static IncrementalValuesProvider<MappingExtensionModelResult> BuildModels(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<CanonicalMappingPairCandidate> candidates)
    {
        return GeneratorStageGuard.Select(
                context,
                candidates,
                MorphantGeneratorStageNames.BuildMappingExtensionModels,
                static (candidate, _) => BuildModel(candidate, candidate.Compilation),
                static candidate => candidate.Pair.Registration.Syntax.GetLocation())
            .WithComparer(MappingExtensionModelResultComparer.Instance)
            .WithTrackingName(MorphantGeneratorStageNames.BuildMappingExtensionModels);
    }

    public static MappingExtensionModelResult BuildModel(
        CanonicalMappingPairCandidate candidate,
        Compilation compilation)
    {
        return new MappingExtensionModelResult(
            MappingExtensionNaming.BuildHintName("MappingExtension", candidate),
            candidate.Pair.Capabilities.Members
                ? MappingExtensionNaming.BuildHintName("MemberExtension", candidate)
                : null,
            PairConfigurationModelBuilder.Build(candidate.Pair, candidate.Surface, compilation));
    }

    public static void RegisterConstruction(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<MappingExtensionModelResult> models)
    {
        var fileScopedNamespace = context.ParseOptionsProvider.Select(
            static (options, _) => ((CSharpParseOptions)options).LanguageVersion >= LanguageVersion.CSharp10);
        var requests = GeneratorStageGuard.SelectTrackedSourceRequest(
            context,
            models.Combine(fileScopedNamespace),
            MorphantGeneratorStageNames.BuildMappingExtensionRequests,
            static (model, _) => BuildConstructionRequest(model.Left, model.Right),
            static _ => Location.None);

        GeneratorStageGuard.RegisterSourceOutput(
            context, requests, "AddMappingExtensionSource",
            static request => request.HintName, AddSource);
    }

    public static void RegisterMembers(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<MappingExtensionModelResult> models)
    {
        var fileScopedNamespace = context.ParseOptionsProvider.Select(
            static (options, _) => ((CSharpParseOptions)options).LanguageVersion >= LanguageVersion.CSharp10);
        var requests = GeneratorStageGuard.SelectTrackedSourceRequest(
            context,
            models.Where(static model => model.MemberHintName is not null).Combine(fileScopedNamespace),
            MorphantGeneratorStageNames.BuildMemberExtensionRequests,
            static (model, _) => BuildMemberRequest(model.Left, model.Right),
            static _ => Location.None);

        GeneratorStageGuard.RegisterSourceOutput(
            context, requests, "AddMemberExtensionSource",
            static request => request.HintName, AddSource);
    }

    public static DslSurfaceRequest BuildConstructionRequest(MappingExtensionModelResult model, bool fileScopedNamespace = false) =>
        new(model.HintName, PairConfigurationEmitter.Emit(model.Model, fileScopedNamespace));

    public static DslSurfaceRequest BuildMemberRequest(MappingExtensionModelResult model, bool fileScopedNamespace = false) =>
        new(model.MemberHintName!, MemberConfigurationEmitter.Emit(model.Model, fileScopedNamespace));

    private static void AddSource(SourceProductionContext context, DslSurfaceRequest request) =>
        context.AddSource(request.HintName, SourceText.From(request.Source, Encoding.UTF8));

    private sealed class MappingExtensionModelResultComparer : IEqualityComparer<MappingExtensionModelResult>
    {
        public static MappingExtensionModelResultComparer Instance { get; } = new();

        public bool Equals(MappingExtensionModelResult left, MappingExtensionModelResult right) =>
            StringComparer.Ordinal.Equals(left.HintName, right.HintName) &&
            StringComparer.Ordinal.Equals(left.MemberHintName, right.MemberHintName) &&
            PairConfigurationModelEquality.Equal(left.Model, right.Model);

        public int GetHashCode(MappingExtensionModelResult value) =>
            StringComparer.Ordinal.GetHashCode(value.HintName);
    }
}

internal readonly record struct MappingExtensionModelResult(
    string HintName,
    string? MemberHintName,
    PairConfigurationModel Model);
