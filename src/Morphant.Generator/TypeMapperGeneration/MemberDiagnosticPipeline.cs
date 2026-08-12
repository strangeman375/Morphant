using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MemberDiagnosticPipeline
{
    public static ImmutableArray<Diagnostic> BuildDiagnostics(
        IEnumerable<MemberDiagnosticCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var distinct = new Dictionary<string, MemberDiagnosticCandidate>(
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
            .ThenBy(static candidate => candidate.MemberOrder)
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
        MemberDiagnosticCandidate candidate)
    {
        var descriptor = candidate.Kind switch
        {
            MemberDiagnosticKind.InvalidRule =>
                MemberDiagnosticDescriptors.InvalidRule,
            MemberDiagnosticKind.RequiredMember =>
                MemberDiagnosticDescriptors.RequiredMember,
            MemberDiagnosticKind.UnavailableLifecycle =>
                MemberDiagnosticDescriptors.UnavailableLifecycle,
            MemberDiagnosticKind.NullMembersPlan =>
                MemberDiagnosticDescriptors.NullMembersPlan,
            _ => throw new InvalidOperationException(
                $"Unknown member diagnostic kind: {candidate.Kind}.")
        };
        object[] arguments = candidate.Kind switch
        {
            MemberDiagnosticKind.InvalidRule =>
                [candidate.MemberName, candidate.Contract, candidate.Reason],
            MemberDiagnosticKind.RequiredMember =>
                [
                    candidate.MemberName,
                    candidate.Contract,
                    ConstructionDiagnosticAnalyzer.FormatPaths(candidate.Paths)
                ],
            MemberDiagnosticKind.UnavailableLifecycle =>
                [
                    candidate.MemberName,
                    candidate.Contract,
                    candidate.Reason,
                    ConstructionDiagnosticAnalyzer.FormatPaths(candidate.Paths)
                ],
            MemberDiagnosticKind.NullMembersPlan =>
                [
                    candidate.Contract,
                    ConstructionDiagnosticAnalyzer.FormatPaths(candidate.Paths)
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
        MemberDiagnosticCandidate candidate,
        MemberDiagnosticCandidate previous)
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

internal enum MemberDiagnosticKind
{
    InvalidRule = 40,
    RequiredMember = 41,
    UnavailableLifecycle = 42,
    NullMembersPlan = 43
}

internal sealed record MemberDiagnosticCandidate(
    MemberDiagnosticKind Kind,
    string Identity,
    string MapperIdentity,
    int LevelOrder,
    string PairKey,
    string OriginIdentity,
    int MemberOrder,
    int Position,
    string Detail,
    bool IsDeclaringOrigin,
    Location PrimaryLocation,
    Location ScopeLocation,
    ImmutableArray<Location> AdditionalLocations,
    string Contract,
    string MemberName,
    string Reason,
    MappingExecutionPathSet Paths);
