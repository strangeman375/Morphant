using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Morphant.Generator.MapperDeclaration;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class IncludeMembersDiagnosticAnalyzer
{
    public static ImmutableArray<IncludeMembersDiagnosticCandidate> Build(
        TypeMapperModel model,
        CancellationToken cancellationToken)
    {
        var result =
            ImmutableArray.CreateBuilder<IncludeMembersDiagnosticCandidate>();

        foreach (var mapping in model.Mappings)
        {
            foreach (var issue in mapping.IncludeMembersIssues.IsDefault
                         ? ImmutableArray<IncludeMembersIssueObservation>.Empty
                         : mapping.IncludeMembersIssues)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var context = mapping.AnalysisContext;
                var primary = issue.Location;
                var additional = issue.AdditionalLocations
                    .Where(location => !SameLocation(location, primary))
                    .GroupBy(LocationIdentity, StringComparer.Ordinal)
                    .Select(static group => group.First())
                    .OrderBy(LocationPath, StringComparer.Ordinal)
                    .ThenBy(static location => location.SourceSpan.Start)
                    .ToImmutableArray();

                result.Add(new IncludeMembersDiagnosticCandidate(
                    issue.Kind,
                    SymbolNameHelper.GetFullMetadataName(
                        context.TargetMapper.OriginalDefinition),
                    context.Identity.Source.Key + "->" +
                    context.Identity.Destination.Key,
                    primary.SourceSpan.Start,
                    issue.Detail,
                    Diagnostic.Create(
                        issue.Kind == IncludeMembersIssueKind.InvalidSelector
                            ? IncludeMembersDiagnosticDescriptors
                                .InvalidSelector
                            : IncludeMembersDiagnosticDescriptors
                                .AmbiguousMember,
                        primary,
                        additional,
                        properties: null,
                        MapperContractDisplay.Create(
                            context.SourceType,
                            context.DestinationType),
                        MapperContractDisplay.CreateType(
                            context.TargetMapper),
                        issue.Detail)));
            }
        }

        return result.ToImmutable();
    }

    private static bool SameLocation(Location left, Location right) =>
        StringComparer.Ordinal.Equals(
            LocationPath(left),
            LocationPath(right)) &&
        left.SourceSpan == right.SourceSpan;

    private static string LocationIdentity(Location location) =>
        LocationPath(location) + "|" + location.SourceSpan.Start + "|" +
        location.SourceSpan.Length;

    private static string LocationPath(Location location) =>
        location.SourceTree?.FilePath ?? string.Empty;
}

internal readonly record struct IncludeMembersDiagnosticCandidate(
    IncludeMembersIssueKind Kind,
    string MapperIdentity,
    string PairKey,
    int Position,
    string Detail,
    Diagnostic Diagnostic);
