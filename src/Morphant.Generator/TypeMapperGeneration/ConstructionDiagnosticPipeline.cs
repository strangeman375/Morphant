using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ConstructionDiagnosticPipeline
{
    public static ImmutableArray<Diagnostic> BuildDiagnostics(
        IEnumerable<ConstructionDiagnosticCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var distinct = new Dictionary<string, ConstructionDiagnosticCandidate>(
            StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (distinct.TryGetValue(candidate.Identity, out var previous))
            {
                var preferred = IsPreferred(candidate, previous)
                    ? candidate
                    : previous;
                distinct[candidate.Identity] = preferred with
                {
                    Paths = candidate.Paths | previous.Paths
                };
            }
            else
            {
                distinct.Add(candidate.Identity, candidate);
            }
        }

        return distinct.Values
            .OrderBy(static candidate => (int)candidate.Kind)
            .ThenBy(
                static candidate => candidate.MapperIdentity,
                StringComparer.Ordinal)
            .ThenBy(
                static candidate => candidate.PairKey,
                StringComparer.Ordinal)
            .ThenBy(
                static candidate => candidate.OriginIdentity,
                StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Position)
            .ThenBy(
                static candidate => candidate.Detail,
                StringComparer.Ordinal)
            .Select(BuildDiagnostic)
            .ToImmutableArray();
    }

    private static Diagnostic BuildDiagnostic(
        ConstructionDiagnosticCandidate candidate)
    {
        var descriptor = candidate.Kind switch
        {
            ConstructionDiagnosticKind.MissingConstructionPolicy =>
                ConstructionDiagnosticDescriptors.MissingConstructionPolicy,
            ConstructionDiagnosticKind.ConventionUnavailable =>
                ConstructionDiagnosticDescriptors.ConventionUnavailable,
            ConstructionDiagnosticKind.InvalidParameterRule =>
                ConstructionDiagnosticDescriptors.InvalidParameterRule,
            ConstructionDiagnosticKind.PreviousUnavailable =>
                ConstructionDiagnosticDescriptors.PreviousUnavailable,
            ConstructionDiagnosticKind.NullConstructionPlan =>
                ConstructionDiagnosticDescriptors.NullConstructionPlan,
            _ => throw new InvalidOperationException(
                $"Unknown construction diagnostic kind: {candidate.Kind}.")
        };
        object[] arguments = candidate.Kind switch
        {
            ConstructionDiagnosticKind.MissingConstructionPolicy or
            ConstructionDiagnosticKind.PreviousUnavailable or
            ConstructionDiagnosticKind.NullConstructionPlan =>
                [
                    candidate.Contract,
                    ConstructionDiagnosticAnalyzer.FormatPaths(candidate.Paths)
                ],
            ConstructionDiagnosticKind.ConventionUnavailable =>
                [
                    candidate.Contract,
                    candidate.Strategy,
                    candidate.Reason
                ],
            ConstructionDiagnosticKind.InvalidParameterRule =>
                [
                    candidate.ParameterName,
                    candidate.Contract,
                    candidate.Reason
                ],
            _ => []
        };

        return Diagnostic.Create(
            descriptor,
            candidate.PrimaryLocation,
            candidate.AdditionalLocations,
            properties: null,
            arguments);
    }

    private static bool IsPreferred(
        ConstructionDiagnosticCandidate candidate,
        ConstructionDiagnosticCandidate previous)
    {
        if (candidate.IsDeclaringOrigin != previous.IsDeclaringOrigin)
        {
            return candidate.IsDeclaringOrigin;
        }

        var mapperComparison = StringComparer.Ordinal.Compare(
            candidate.MapperIdentity,
            previous.MapperIdentity);

        if (mapperComparison != 0)
        {
            return mapperComparison < 0;
        }

        return candidate.LevelOrder < previous.LevelOrder;
    }
}

internal enum ConstructionDiagnosticKind
{
    MissingConstructionPolicy = 35,
    ConventionUnavailable = 36,
    InvalidParameterRule = 37,
    PreviousUnavailable = 38,
    NullConstructionPlan = 39
}

internal sealed record ConstructionDiagnosticCandidate(
    ConstructionDiagnosticKind Kind,
    string Identity,
    string MapperIdentity,
    int LevelOrder,
    string PairKey,
    string OriginIdentity,
    int Position,
    string Detail,
    bool IsDeclaringOrigin,
    Location PrimaryLocation,
    Location ScopeLocation,
    ImmutableArray<Location> AdditionalLocations,
    string Contract,
    string ParameterName,
    string Strategy,
    string Reason,
    MappingExecutionPathSet Paths);
