using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class CallbackDiagnosticPipeline
{
    public static ImmutableArray<Diagnostic> BuildDiagnostics(
        IEnumerable<CallbackDiagnosticCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var distinct = new Dictionary<string, CallbackDiagnosticCandidate>(
            StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!distinct.TryGetValue(candidate.Identity, out var previous) ||
                IsPreferred(candidate, previous))
            {
                distinct[candidate.Identity] = candidate;
            }
        }

        return distinct.Values
            .OrderBy(static candidate => candidate.IdOrder)
            .ThenBy(
                static candidate => candidate.MapperIdentity,
                StringComparer.Ordinal)
            .ThenBy(
                static candidate => candidate.PairKey,
                StringComparer.Ordinal)
            .ThenBy(
                static candidate => candidate.CallbackOriginIdentity,
                StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Position)
            .ThenBy(
                static candidate => candidate.Detail,
                StringComparer.Ordinal)
            .Select(static candidate => candidate.Diagnostic)
            .ToImmutableArray();
    }

    private static bool IsPreferred(
        CallbackDiagnosticCandidate candidate,
        CallbackDiagnosticCandidate previous)
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

internal sealed record CallbackDiagnosticCandidate(
    int IdOrder,
    string Identity,
    string CallbackOriginIdentity,
    string MapperIdentity,
    int LevelOrder,
    string PairKey,
    int Position,
    string Detail,
    bool IsDeclaringOrigin,
    Diagnostic Diagnostic);
