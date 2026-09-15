using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class UserExpressionLayout
{
    private const string AnnotationKind = "MorphantUserExpressionLayout";

    public static SyntaxNode Preserve(SyntaxNode source, SyntaxNode rewritten)
    {
        if (source is ExpressionSyntax &&
            (source is SwitchExpressionSyntax ||
                source.GetLocation().GetLineSpan() is var span &&
                span.StartLinePosition.Line != span.EndLinePosition.Line))
        {
            return rewritten.WithAdditionalAnnotations(
                new SyntaxAnnotation(AnnotationKind, GetIndentation(source)));
        }

        return rewritten;
    }

    public static ExpressionSyntax ParseExpression(string expression)
    {
        var syntax = SyntaxFactory.ParseExpression(expression);
        return HasLineBreak(expression) || ContainsSwitchExpression(syntax)
            ? syntax.WithAdditionalAnnotations(new SyntaxAnnotation(AnnotationKind, string.Empty))
            : syntax;
    }

    public static T Restore<T>(T original, T normalized)
        where T : SyntaxNode
    {
        normalized = RestoreNullableSpacing(original, normalized);
        var replacements = new Dictionary<SyntaxNode, SyntaxNode>();
        foreach (var expression in original.DescendantNodes().OfType<ExpressionSyntax>()
                     .Where(node => node.HasAnnotations(AnnotationKind) &&
                         !node.Ancestors().Any(parent => parent.HasAnnotations(AnnotationKind))))
        {
            var annotation = expression.GetAnnotations(AnnotationKind).First();
            var target = normalized.GetAnnotatedNodes(annotation).FirstOrDefault();
            if (target is null) continue;

            var preserved = Normalize(expression);
            var indentation = GetIndentation(target);
            var tokens = preserved.DescendantTokens().ToArray();
            preserved = preserved.ReplaceTokens(tokens, (token, _) => token
                .WithLeadingTrivia(IndentTrivia(token.LeadingTrivia, indentation))
                .WithTrailingTrivia(IndentTrivia(token.TrailingTrivia, indentation)));
            replacements.Add(target, preserved.WithTriviaFrom(target));
        }

        return normalized.ReplaceNodes(replacements.Keys, (node, _) => replacements[node]);
    }

    private static SyntaxTriviaList IndentTrivia(SyntaxTriviaList trivia, string indentation)
    {
        var lines = trivia.ToFullString().Replace("\r\n", "\n").Split('\n');
        for (var index = 1; index < lines.Length; index++)
            lines[index] = index == lines.Length - 1 || !string.IsNullOrWhiteSpace(lines[index])
                ? indentation + lines[index]
                : string.Empty;
        return SyntaxFactory.ParseLeadingTrivia(string.Join("\r\n", lines));
    }

    public static string Indent(string expression, string indentation)
    {
        var syntax = SyntaxFactory.ParseExpression(expression);
        return indentation + syntax.ReplaceTokens(syntax.DescendantTokens(), (token, _) => token
            .WithLeadingTrivia(IndentTrivia(token.LeadingTrivia, indentation))
            .WithTrailingTrivia(IndentTrivia(token.TrailingTrivia, indentation))).ToFullString();
    }

    public static T Normalize<T>(T syntax, SyntaxNode? source = null)
        where T : CSharpSyntaxNode
    {
        var original = syntax.WithoutTrivia();
        var normalized = RestoreNullableSpacing(original,
            original.NormalizeWhitespace(indentation: "    ", eol: "\r\n"));
        var originalTokens = original.DescendantTokens().ToArray();

        if (!ContainsSwitchExpression(original) && !originalTokens.Any(token =>
                HasLineBreak(token.LeadingTrivia.ToFullString()) ||
                HasLineBreak(token.TrailingTrivia.ToFullString())))
        {
            return normalized;
        }

        var normalizedTokens = normalized.DescendantTokens().ToArray();
        var indentation = source is not null
            ? GetIndentation(source)
            : syntax.GetAnnotations(AnnotationKind).FirstOrDefault()?.Data ?? GetIndentation(syntax);
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

    private static T RestoreNullableSpacing<T>(T original, T normalized) where T : SyntaxNode
    {
        var before = original.DescendantTokens().ToArray();
        var after = normalized.DescendantTokens().ToArray();
        if (before.Length != after.Length) return normalized;
        var replacements = new Dictionary<SyntaxToken, SyntaxToken>();
        for (var index = 1; index < before.Length; index++)
        {
            if (before[index - 1].Parent is not NullableTypeSyntax ||
                !before[index - 1].IsKind(SyntaxKind.QuestionToken) ||
                !before[index].IsKind(SyntaxKind.CloseParenToken) ||
                before[index - 1].TrailingTrivia.ToFullString().Length != 0 ||
                before[index].LeadingTrivia.ToFullString().Length != 0)
                continue;

            // Normalization can insert a space after the nullable '?' even
            // though these tokens already form valid synthesized syntax.
            replacements[after[index - 1]] = after[index - 1].WithTrailingTrivia(default(SyntaxTriviaList));
            replacements[after[index]] = after[index].WithLeadingTrivia(default(SyntaxTriviaList));
        }
        return normalized.ReplaceTokens(replacements.Keys, (token, _) => replacements[token]);
    }

    public static bool HasLineBreak(string text) =>
        text.IndexOf('\r') >= 0 || text.IndexOf('\n') >= 0;

    public static bool ContainsSwitchExpression(SyntaxNode syntax) =>
        syntax.DescendantNodesAndSelf().Any(static node => node is SwitchExpressionSyntax);

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
        return normalized.Length == 0 && original.Length == 0
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
