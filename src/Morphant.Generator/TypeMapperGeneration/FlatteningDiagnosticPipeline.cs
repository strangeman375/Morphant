using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class FlatteningDiagnosticPipeline
{
    public static ImmutableArray<Diagnostic> BuildDiagnostics(
        IEnumerable<FlatteningDiagnosticCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var candidate in candidates
                     .OrderBy(static candidate => candidate.MapperIdentity,
                         StringComparer.Ordinal)
                     .ThenBy(static candidate => candidate.PairKey,
                         StringComparer.Ordinal)
                     .ThenBy(static candidate => candidate.Position)
                     .ThenBy(static candidate => candidate.Detail,
                         StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var location = candidate.Diagnostic.Location;
            var identity = candidate.MapperIdentity + "|" +
                           candidate.PairKey + "|" +
                           (location.SourceTree?.FilePath ?? string.Empty) +
                           "|" + location.SourceSpan.Start + "|" +
                           candidate.Detail;

            if (seen.Add(identity))
            {
                result.Add(candidate.Diagnostic);
            }
        }

        return result.ToImmutable();
    }
}
