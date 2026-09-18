using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator;

internal static class DiagnosticLocationActualizer
{
    public static ImmutableArray<Diagnostic> Actualize(
        ImmutableArray<Diagnostic> diagnostics,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        if (diagnostics.IsEmpty)
        {
            return diagnostics;
        }

        var result = ImmutableArray.CreateBuilder<Diagnostic>(
            diagnostics.Length);
        var changes = new Dictionary<(SyntaxTree Previous, SyntaxTree Current), IList<TextChange>>();

        foreach (var diagnostic in diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var location = Actualize(
                diagnostic.Location,
                compilation,
                changes,
                cancellationToken);
            var additionalLocations = diagnostic.AdditionalLocations
                .Select(candidate => Actualize(
                    candidate,
                    compilation,
                    changes,
                    cancellationToken))
                .ToImmutableArray();

            result.Add(
                LocationsEqual(
                    diagnostic.Location,
                    location) &&
                LocationsEqual(
                    diagnostic.AdditionalLocations,
                    additionalLocations)
                    ? diagnostic
                    : ActualizeLocations(
                        diagnostic,
                        location,
                        additionalLocations));
        }

        return result.ToImmutable();
    }

    private static Location Actualize(
        Location location,
        Compilation compilation,
        Dictionary<(SyntaxTree Previous, SyntaxTree Current), IList<TextChange>> changes,
        CancellationToken cancellationToken)
    {
        if (!location.IsInSource ||
            location.SourceTree is not { } previousTree ||
            compilation.ContainsSyntaxTree(previousTree))
        {
            return location;
        }

        var previousText = previousTree.GetText(cancellationToken);
        var span = location.SourceSpan;
        Location? matchingSpanLocation = null;
        foreach (var currentTree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!StringComparer.Ordinal.Equals(
                    currentTree.FilePath,
                    previousTree.FilePath))
            {
                continue;
            }

            var currentText = currentTree.GetText(cancellationToken);

            if (previousText.ContentEquals(currentText))
            {
                return Location.Create(currentTree, span);
            }

            var key = (previousTree, currentTree);
            if (!changes.TryGetValue(key, out var treeChanges))
            {
                treeChanges = currentTree.GetChanges(previousTree);
                changes.Add(key, treeChanges);
            }

            // A cached contract can retain locations across body/trivia edits.
            // Translate only untouched spans; a matching word at the old offset
            // may now belong to a different declaration or even a comment.
            var offset = 0;
            var overlaps = false;
            foreach (var change in treeChanges)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (change.Span.End <= span.Start)
                    offset += (change.NewText?.Length ?? 0) - change.Span.Length;
                else if (change.Span.Start < span.End)
                    overlaps = true;
            }

            if (overlaps || span.Start + offset < 0) continue;
            var currentSpan = new TextSpan(span.Start + offset, span.Length);
            if (currentSpan.End <= currentText.Length &&
                TextMatchesAtSpan(previousText, currentText, span, currentSpan))
                matchingSpanLocation ??= Location.Create(currentTree, currentSpan);
        }

        return matchingSpanLocation ?? Location.None;
    }

    private static bool TextMatchesAtSpan(
        SourceText previous,
        SourceText current,
        TextSpan span,
        TextSpan currentSpan)
    {
        if (span.End > previous.Length)
        {
            return false;
        }

        for (var offset = 0; offset < span.Length; offset++)
        {
            if (previous[span.Start + offset] !=
                current[currentSpan.Start + offset])
            {
                return false;
            }
        }

        return true;
    }

    private static Diagnostic ActualizeLocations(
        Diagnostic diagnostic,
        Location location,
        ImmutableArray<Location> additionalLocations)
    {
        var descriptor = diagnostic.Descriptor;

        return Diagnostic.Create(
            diagnostic.Id,
            descriptor.Category,
            diagnostic.GetMessage(CultureInfo.InvariantCulture),
            diagnostic.Severity,
            diagnostic.DefaultSeverity,
            descriptor.IsEnabledByDefault,
            diagnostic.WarningLevel,
            diagnostic.IsSuppressed,
            descriptor.Title,
            descriptor.Description,
            descriptor.HelpLinkUri,
            location,
            additionalLocations,
            descriptor.CustomTags,
            diagnostic.Properties);
    }

    private static bool LocationsEqual(
        Location left,
        Location right)
    {
        return ReferenceEquals(left.SourceTree, right.SourceTree) &&
               left.SourceSpan == right.SourceSpan &&
               left.Kind == right.Kind;
    }

    private static bool LocationsEqual(
        IReadOnlyList<Location> left,
        ImmutableArray<Location> right)
    {
        if (left.Count != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!LocationsEqual(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }
}
