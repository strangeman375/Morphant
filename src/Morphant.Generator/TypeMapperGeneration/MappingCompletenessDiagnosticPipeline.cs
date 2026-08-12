using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MappingCompletenessDiagnosticPipeline
{
    public static ImmutableArray<Diagnostic> BuildDiagnostics(
        IEnumerable<MappingCompletenessDiagnosticCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var distinct =
            new Dictionary<string, MappingCompletenessDiagnosticCandidate>(
                StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            distinct.TryAdd(candidate.Identity, candidate);
        }

        return distinct.Values
            .OrderBy(static candidate => (int)candidate.Kind)
            .ThenBy(
                static candidate => candidate.MapperIdentity,
                StringComparer.Ordinal)
            .ThenBy(
                static candidate => candidate.PairKey,
                StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.MemberOrder)
            .ThenBy(
                static candidate => candidate.MemberIdentity,
                StringComparer.Ordinal)
            .Select(BuildDiagnostic)
            .ToImmutableArray();
    }

    private static Diagnostic BuildDiagnostic(
        MappingCompletenessDiagnosticCandidate candidate)
    {
        var descriptor = candidate.Kind switch
        {
            MappingCompletenessDiagnosticKind.SourceMemberUnused =>
                MappingCompletenessDiagnosticDescriptors.SourceMemberUnused,
            MappingCompletenessDiagnosticKind.DestinationMemberUnmapped =>
                MappingCompletenessDiagnosticDescriptors
                    .DestinationMemberUnmapped,
            _ => throw new InvalidOperationException(
                $"Unknown mapping completeness diagnostic kind: " +
                $"{candidate.Kind}.")
        };

        return Diagnostic.Create(
            descriptor,
            candidate.PrimaryLocation,
            candidate.AdditionalLocations,
            properties: null,
            candidate.MemberDisplay,
            candidate.Contract);
    }
}

internal enum MappingCompletenessDiagnosticKind
{
    SourceMemberUnused = 47,
    DestinationMemberUnmapped = 48
}

internal sealed record MappingCompletenessDiagnosticCandidate(
    MappingCompletenessDiagnosticKind Kind,
    string Identity,
    string MapperIdentity,
    string PairKey,
    string MemberIdentity,
    int MemberOrder,
    Location PrimaryLocation,
    ImmutableArray<Location> AdditionalLocations,
    string MemberDisplay,
    string Contract);
