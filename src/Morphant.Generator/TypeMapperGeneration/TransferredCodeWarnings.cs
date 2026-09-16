using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.TypeMapperGeneration;

internal readonly record struct TransferredWarningSuppression(
    string Origin, string DiagnosticId, bool WholeLine);

// Warning origins survive the string-based mapping plans as internal trivia.
// They are removed before publishing source and never identify convention code.
internal static class TransferredCodeWarnings
{
    private const string AnnotationKind = "Morphant.TransferWarning";
    private const string Prefix = "/*Morphant.TransferWarning:";
    private static readonly ConditionalWeakTable<SemanticModel, SourceWarnings> Sources = new();

    public static SyntaxNode Annotate(SyntaxNode source, SyntaxNode rewritten, SemanticModel model)
    {
        var data = GetAnnotation(source, model);
        if (data is null) return rewritten;
        var token = rewritten.GetFirstToken();
        return rewritten.ReplaceToken(token, token.WithAdditionalAnnotations(new SyntaxAnnotation(AnnotationKind, data)));
    }

    public static string? GetAnnotation(SyntaxNode source, SemanticModel model)
        => GetAnnotation(source.SyntaxTree, source.SpanStart, model);

    public static SyntaxToken Annotate(SyntaxToken source, SyntaxToken rewritten, SemanticModel model)
    {
        var data = GetAnnotation(source.SyntaxTree, source.SpanStart, model);
        return data is null ? rewritten : rewritten.WithAdditionalAnnotations(
            new SyntaxAnnotation(AnnotationKind, data));
    }

    private static string? GetAnnotation(SyntaxTree? tree, int position, SemanticModel model)
    {
        if (tree != model.SyntaxTree) return null;
        var warnings = Sources.GetValue(model, static semanticModel => new SourceWarnings(semanticModel));
        var ids = warnings.At(position);
        if (!model.GetNullableContext(position).WarningsEnabled())
            ids = ids.Add("nullable");
        return ids.IsEmpty ? null : warnings.Identity + "_" + position + ":" + string.Join(",", ids);
    }

    public static string AnnotateReference(string reference, string? annotation)
    {
        if (annotation is null) return reference;
        var syntax = SyntaxFactory.ParseExpression(reference)
            .WithTrailingTrivia(SyntaxFactory.Space);
        var token = syntax.GetFirstToken();
        return Serialize(syntax.ReplaceToken(token, token.WithAdditionalAnnotations(
            new SyntaxAnnotation(AnnotationKind, annotation)))).TrimEnd();
    }

    public static string Serialize(SyntaxNode syntax)
    {
        var text = syntax.ToFullString();
        var changes = new List<TextChange>();
        foreach (var token in syntax.DescendantTokens())
        {
            var annotation = token.GetAnnotations(AnnotationKind).LastOrDefault();
            if (annotation is null) continue;
            var position = token.Span.End - syntax.FullSpan.Start;
            var gap = new string(text.Skip(position).TakeWhile(character => character is ' ' or '\t').ToArray());
            changes.Add(new TextChange(new TextSpan(position, 0), Prefix + annotation.Data + ":" +
                Convert.ToBase64String(Encoding.UTF8.GetBytes(gap)) + "*/"));
        }
        return changes.Count == 0 ? text : SourceText.From(text).WithChanges(changes).ToString();
    }

    public static string? GetOrigin(Diagnostic diagnostic)
    {
        var token = diagnostic.Location.SourceTree!.GetRoot().FindToken(diagnostic.Location.SourceSpan.Start);
        if (token.SpanStart != diagnostic.Location.SourceSpan.Start) return null;
        foreach (var trivia in token.TrailingTrivia.Concat(token.GetNextToken().LeadingTrivia))
        {
            if (!TryRead(trivia, out var origin, out var ids, out _)) continue;
            if (ids.Contains(diagnostic.Id) || ids.Contains("nullable") &&
                MappingExpressionCompatibility.IsNullableWarning(diagnostic.Id)) return origin;
        }
        return null;
    }

    public static bool IsObsoleteWarning(Diagnostic diagnostic) =>
        diagnostic.DefaultSeverity == DiagnosticSeverity.Warning &&
        (diagnostic.Id is "CS0612" or "CS0618" ||
         diagnostic.Descriptor.CustomTags.Contains("CustomObsolete"));

    public static TextSpan GetSuppressionSpan(Diagnostic diagnostic, bool wholeLine) =>
        GetSuppressionSpan(
            diagnostic.Location.SourceTree!.GetRoot().FindToken(diagnostic.Location.SourceSpan.Start),
            diagnostic.Location.SourceTree.GetText(), wholeLine);

    private static TextSpan GetSuppressionSpan(SyntaxToken token, SourceText text, bool wholeLine)
    {
        // Directives inside interpolations are invalid C#; inside string text
        // they would silently change the value. Enclose the outermost string.
        var interpolation = token.Parent?.AncestorsAndSelf()
            .OfType<InterpolatedStringExpressionSyntax>().LastOrDefault();
        var reference = interpolation?.Span ?? TextSpan.FromBounds(token.SpanStart,
            token.Parent?.AncestorsAndSelf()
                .Where(node => node.SpanStart == token.SpanStart &&
                    node is NameSyntax or MemberAccessExpressionSyntax)
                .Select(node => node.Span.End).DefaultIfEmpty(token.Span.End).Max()
                ?? token.Span.End);
        var firstLine = text.Lines.GetLineFromPosition(reference.Start);
        var lastLine = text.Lines.GetLineFromPosition(reference.End);
        var root = token.Parent!.SyntaxTree.GetRoot();
        if (wholeLine && IsDirectiveBoundary(firstLine.Start) &&
            IsDirectiveBoundary(lastLine.EndIncludingLineBreak))
            return TextSpan.FromBounds(firstLine.Start, lastLine.EndIncludingLineBreak);

        var start = text.ToString(TextSpan.FromBounds(firstLine.Start, reference.Start))
            .All(char.IsWhiteSpace) ? firstLine.Start : reference.Start;
        return TextSpan.FromBounds(start, reference.End);

        bool IsDirectiveBoundary(int position)
        {
            var boundaryToken = root.FindToken(position);
            if (boundaryToken.SpanStart < position && position < boundaryToken.Span.End)
                return false;
            if (boundaryToken.Parent!.AncestorsAndSelf().OfType<InterpolatedStringExpressionSyntax>()
                .Any(value => value.SpanStart < position && position < value.Span.End))
                return false;
            var trivia = root.FindTrivia(position);
            return !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                position <= trivia.SpanStart || position >= trivia.Span.End;
        }
    }

    public static string Apply(string source, IEnumerable<TransferredWarningSuppression> suppressions, bool retainOrigins)
    {
        if (source.IndexOf(Prefix, StringComparison.Ordinal) < 0) return source;
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var tokens = root.DescendantTokens().ToArray();
        var sites = new List<(int Token, string Origin)>();
        var removals = new List<TextChange>();
        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            foreach (var trivia in token.TrailingTrivia.Concat(token.GetNextToken().LeadingTrivia))
            {
                if (!TryRead(trivia, out var origin, out _, out var gap)) continue;
                sites.Add((index, origin));
                if (retainOrigins) continue;
                var start = trivia.SpanStart;
                while (start > token.Span.End && source[start - 1] is ' ' or '\t') start--;
                var end = trivia.Span.End;
                while (end < source.Length && source[end] is ' ' or '\t') end++;
                removals.Add(new TextChange(TextSpan.FromBounds(start, end), gap));
            }
        }
        var text = SourceText.From(source);
        if (removals.Count > 0)
        {
            text = text.WithChanges(removals);
            tokens = CSharpSyntaxTree.ParseText(text).GetRoot().DescendantTokens().ToArray();
        }
        var ranges = new List<(int Start, int End, string Id)>();
        var decisions = suppressions
            .GroupBy(item => (item.Origin, item.DiagnosticId))
            .Select(group => new TransferredWarningSuppression(
                group.Key.Origin, group.Key.DiagnosticId,
                group.All(item => item.WholeLine)))
            .ToLookup(item => item.Origin, StringComparer.Ordinal);
        foreach (var site in sites)
        {
            var token = tokens[site.Token];
            foreach (var decision in decisions[site.Origin])
            {
                var span = GetSuppressionSpan(token, text, decision.WholeLine);
                ranges.Add((span.Start, span.End, decision.DiagnosticId));
            }
        }
        var insertions = new SortedDictionary<int, List<string>>();
        foreach (var group in ranges.Distinct().GroupBy(range => range.Id))
        {
            var merged = new List<(int Start, int End)>();
            foreach (var range in group.OrderBy(range => range.Start).ThenBy(range => range.End))
            {
                if (merged.Count > 0 && merged[merged.Count - 1].End >= range.Start)
                    merged[merged.Count - 1] = (merged[merged.Count - 1].Start, Math.Max(merged[merged.Count - 1].End, range.End));
                else merged.Add((range.Start, range.End));
            }
            foreach (var range in merged)
            {
                var line = text.Lines.GetLineFromPosition(range.Start);
                var indentation = new string(line.ToString()
                    .TakeWhile(character => character is ' ' or '\t').ToArray());
                AddDirective(range.Start, "disable", group.Key, indentation);
                AddDirective(range.End, "restore", group.Key, indentation);
            }
        }
        return text.WithChanges(insertions.Select(pair =>
        {
            var end = pair.Key;
            if (pair.Key != text.Lines.GetLineFromPosition(pair.Key).Start)
            {
                while (end < text.Length && text[end] is ' ' or '\t') end++;
            }
            return new TextChange(TextSpan.FromBounds(pair.Key, end), string.Concat(pair.Value));
        })).ToString();

        void AddDirective(int position, string action, string id, string indentation)
        {
            var line = text.Lines.GetLineFromPosition(position);
            var atStart = position == line.Start;
            var directive = (atStart ? string.Empty : "\r\n") + indentation +
                "#pragma warning " + action + " " + id + "\r\n" + (atStart ? string.Empty : indentation);
            if (!insertions.TryGetValue(position, out var values)) insertions.Add(position, values = new List<string>());
            values.Add(directive);
        }
    }

    private static bool TryRead(SyntaxTrivia trivia, out string origin, out string[] ids, out string gap)
    {
        origin = gap = string.Empty;
        ids = Array.Empty<string>();
        var text = trivia.ToString();
        if (!trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
            !text.StartsWith(Prefix, StringComparison.Ordinal) ||
            !text.EndsWith("*/", StringComparison.Ordinal) ||
            text.Length < Prefix.Length + 2) return false;
        var parts = text.Substring(Prefix.Length, text.Length - Prefix.Length - 2).Split(':');
        if (parts.Length != 3) return false;
        origin = parts[0];
        ids = parts[1].Split(',');
        try
        {
            gap = Encoding.UTF8.GetString(Convert.FromBase64String(parts[2]));
        }
        catch (FormatException)
        {
            return false;
        }
        return true;
    }

    private sealed class SourceWarnings
    {
        private readonly ILookup<int, string> _warnings;
        public string Identity { get; }

        public SourceWarnings(SemanticModel model)
        {
            Identity = string.Concat(model.SyntaxTree.GetText().GetChecksum().Select(value => value.ToString("x2")));
            var compilation = model.Compilation.WithOptions(model.Compilation.Options.WithReportSuppressedDiagnostics(true));
            _warnings = compilation.GetSemanticModel(model.SyntaxTree).GetDiagnostics()
                .Where(diagnostic => diagnostic.DefaultSeverity == DiagnosticSeverity.Warning &&
                    diagnostic.Location.SourceTree == model.SyntaxTree)
                .ToLookup(diagnostic => diagnostic.Location.SourceSpan.Start, diagnostic => diagnostic.Id);
        }

        public ImmutableArray<string> At(int position) => _warnings[position].Distinct(StringComparer.Ordinal).ToImmutableArray();
    }
}
