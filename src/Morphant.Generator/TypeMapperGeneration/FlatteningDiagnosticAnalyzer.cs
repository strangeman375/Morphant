using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Morphant.Generator.MapperDeclaration;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class FlatteningDiagnosticAnalyzer
{
    public static ImmutableArray<FlatteningDiagnosticCandidate> Build(
        TypeMapperModel model,
        CancellationToken cancellationToken)
    {
        var result =
            ImmutableArray.CreateBuilder<FlatteningDiagnosticCandidate>();

        foreach (var mapping in model.Mappings)
        {
            AnalyzeMapping(mapping);
        }

        return result.ToImmutable();

        void AnalyzeMapping(TypeMapperMappingModel mapping)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddIssues(mapping, mapping.MemberObservation?.FlatteningIssues);
            AddIssues(
                mapping,
                mapping.ConstructorObservation?.FlatteningIssues);

            if (mapping.ControlFlow is not { } controlFlow)
            {
                return;
            }

            Visit(controlFlow.CreateRoot);
            Visit(controlFlow.UpdateRoot);
        }

        void Visit(TypeMapperControlFlowNode node)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (node.Leaf is { } leaf)
            {
                AnalyzeMapping(leaf);
            }

            if (node.WhenTrue is { } whenTrue)
            {
                Visit(whenTrue);
            }

            if (node.WhenFalse is { } whenFalse)
            {
                Visit(whenFalse);
            }

            foreach (var section in node.SwitchSections.IsDefault
                         ? ImmutableArray<TypeMapperSwitchSectionModel>.Empty
                         : node.SwitchSections)
            {
                Visit(section.Branch);
            }

            if (node.SwitchContinuation is { } switchContinuation)
            {
                Visit(switchContinuation);
            }

            if (node.EvaluationContinuation is { } evaluationContinuation)
            {
                Visit(evaluationContinuation);
            }
        }

        void AddIssues(
            TypeMapperMappingModel mapping,
            ImmutableArray<FlatteningIssueObservation>? issues)
        {
            if (issues is null || issues.Value.IsDefaultOrEmpty)
            {
                return;
            }

            foreach (var issue in issues.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var primary = issue.OriginNode?.GetLocation() ??
                    issue.TargetSymbol.Locations.FirstOrDefault(
                        static location => location.IsInSource) ??
                    mapping.AnalysisContext.Registration.Syntax.GetLocation();
                var additional = issue.CandidateLocations
                    .Where(location => !SameLocation(location, primary))
                    .ToImmutableArray();
                var detail = "target '" + issue.TargetName +
                    "' matches " + string.Join(
                        ", ",
                        issue.CandidatePaths.Select(static path =>
                            "'" + path + "'"));
                var context = mapping.AnalysisContext;

                result.Add(new FlatteningDiagnosticCandidate(
                    SymbolNameHelper.GetFullMetadataName(
                        context.TargetMapper.OriginalDefinition),
                    context.Identity.Source.Key + "->" +
                    context.Identity.Destination.Key,
                    primary.SourceSpan.Start,
                    detail,
                    Diagnostic.Create(
                        FlatteningDiagnosticDescriptors.AmbiguousPath,
                        primary,
                        additional,
                        properties: null,
                        MapperContractDisplay.Create(
                            context.SourceType,
                            context.DestinationType),
                        MapperContractDisplay.CreateType(
                            context.TargetMapper),
                        detail)));
            }
        }
    }

    private static bool SameLocation(Location left, Location right) =>
        StringComparer.Ordinal.Equals(
            left.SourceTree?.FilePath,
            right.SourceTree?.FilePath) &&
        left.SourceSpan == right.SourceSpan;
}

internal readonly record struct FlatteningDiagnosticCandidate(
    string MapperIdentity,
    string PairKey,
    int Position,
    string Detail,
    Diagnostic Diagnostic);
