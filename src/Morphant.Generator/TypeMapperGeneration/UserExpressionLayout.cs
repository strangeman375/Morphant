using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class UserExpressionLayout
{
    public static T Normalize<T>(T syntax, SyntaxNode? source = null)
        where T : CSharpSyntaxNode
    {
        var original = syntax.WithoutTrivia();
        var normalized = original.NormalizeWhitespace(indentation: "    ", eol: "\r\n");
        var originalTokens = original.DescendantTokens().ToArray();

        if (!originalTokens.Any(token =>
                HasLineBreak(token.LeadingTrivia.ToFullString()) ||
                HasLineBreak(token.TrailingTrivia.ToFullString())))
        {
            return normalized;
        }

        var normalizedTokens = normalized.DescendantTokens().ToArray();
        var indentation = GetIndentation(source ?? syntax);
        var replacements = new Dictionary<SyntaxToken, SyntaxToken>();

        for (var index = 0; index < normalizedTokens.Length; index++)
        {
            var gap = index == 0
                ? string.Empty
                : originalTokens[index - 1].TrailingTrivia.ToFullString() +
                  originalTokens[index].LeadingTrivia.ToFullString();
            var trivia = index == 0
                ? default
                : PreserveGap(gap, normalizedTokens[index - 1], normalizedTokens[index], indentation);
            replacements.Add(normalizedTokens[index], normalizedTokens[index]
                .WithLeadingTrivia(trivia)
                .WithTrailingTrivia(default(SyntaxTriviaList)));
        }

        return normalized.ReplaceTokens(normalizedTokens,
            (token, _) => replacements[token]);
    }

    public static bool HasLineBreak(string text) =>
        text.IndexOf('\r') >= 0 || text.IndexOf('\n') >= 0;

    private static SyntaxTriviaList PreserveGap(
        string original,
        SyntaxToken previous,
        SyntaxToken current,
        string indentation)
    {
        if (HasLineBreak(original) || original.Any(character => !char.IsWhiteSpace(character)))
        {
            var lines = original.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var index = 1; index < lines.Length; index++)
            {
                if (lines[index].StartsWith(indentation, StringComparison.Ordinal))
                    lines[index] = lines[index].Substring(indentation.Length);
            }

            return SyntaxFactory.ParseLeadingTrivia(string.Join("\r\n", lines));
        }

        // Synthesized tokens still need their normal lexical separators, but
        // normalization must not insert new breaks into a user-written layout.
        var normalized = previous.TrailingTrivia.ToFullString() + current.LeadingTrivia.ToFullString();
        return normalized.Length == 0
            ? default
            : SyntaxFactory.TriviaList(SyntaxFactory.Space);
    }

    private static string GetIndentation(SyntaxNode syntax)
    {
        var text = syntax.SyntaxTree.GetText();
        var line = text.Lines.GetLineFromPosition(syntax.SpanStart);
        var prefix = text.ToString(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(line.Start, syntax.SpanStart));
        return new string(prefix.TakeWhile(character => character is ' ' or '\t').ToArray());
    }
}
