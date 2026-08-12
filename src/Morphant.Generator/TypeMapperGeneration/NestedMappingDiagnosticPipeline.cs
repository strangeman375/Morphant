using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class NestedMappingDiagnosticPipeline
{
    public static ImmutableArray<Diagnostic> BuildDiagnostics(
        IEnumerable<NestedMappingDiagnosticCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var distinct =
            new Dictionary<string, NestedMappingDiagnosticCandidate>(
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
        NestedMappingDiagnosticCandidate candidate)
    {
        var descriptor = candidate.Kind switch
        {
            NestedMappingDiagnosticKind.PairUnknown =>
                NestedMappingDiagnosticDescriptors.PairUnknown,
            NestedMappingDiagnosticKind.ResultIncompatible =>
                NestedMappingDiagnosticDescriptors.ResultIncompatible,
            NestedMappingDiagnosticKind.UpdateDestinationInvalid =>
                NestedMappingDiagnosticDescriptors.UpdateDestinationInvalid,
            _ => throw new InvalidOperationException(
                $"Unknown nested mapping diagnostic kind: " +
                $"{candidate.Kind}.")
        };
        object[] arguments = candidate.Kind switch
        {
            NestedMappingDiagnosticKind.PairUnknown or
            NestedMappingDiagnosticKind.UpdateDestinationInvalid =>
                [
                    candidate.Marker,
                    candidate.Contract,
                    candidate.Reason,
                    ConstructionDiagnosticAnalyzer.FormatPaths(
                        candidate.Paths)
                ],
            NestedMappingDiagnosticKind.ResultIncompatible =>
                [
                    candidate.NestedDestinationType,
                    candidate.Contract,
                    candidate.TargetType,
                    ConstructionDiagnosticAnalyzer.FormatPaths(
                        candidate.Paths)
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
        NestedMappingDiagnosticCandidate candidate,
        NestedMappingDiagnosticCandidate previous)
    {
        if (candidate.IsDeclaringOrigin != previous.IsDeclaringOrigin)
        {
            return candidate.IsDeclaringOrigin;
        }

        var mapperComparison = StringComparer.Ordinal.Compare(
            candidate.MapperIdentity,
            previous.MapperIdentity);

        return mapperComparison != 0
            ? mapperComparison < 0
            : candidate.LevelOrder < previous.LevelOrder;
    }
}

internal enum NestedMappingDiagnosticKind
{
    PairUnknown = 44,
    ResultIncompatible = 45,
    UpdateDestinationInvalid = 46
}

internal sealed record NestedMappingDiagnosticCandidate(
    NestedMappingDiagnosticKind Kind,
    string Identity,
    string MapperIdentity,
    int LevelOrder,
    string PairKey,
    string OriginIdentity,
    int Position,
    string Detail,
    bool IsDeclaringOrigin,
    Location PrimaryLocation,
    ImmutableArray<Location> AdditionalLocations,
    string Contract,
    string Marker,
    string Reason,
    string NestedDestinationType,
    string TargetType,
    MappingExecutionPathSet Paths);
