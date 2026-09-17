using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

// Inferred names must survive temporary local identities in string-based plans.
// Remove only synthesized labels once the final variable names are known.
internal static class TransferredProjectionNames
{
    private const string Marker = "/*Morphant.InferredName*/";

    public static T Mark<T>(T name) where T : SyntaxNode
    {
        var token = name.GetFirstToken();
        var trivia = token.TrailingTrivia;
        if (name is NameEqualsSyntax) trivia = trivia.Add(SyntaxFactory.Space);
        return name.ReplaceToken(token, token.WithTrailingTrivia(trivia.Add(SyntaxFactory.Comment(Marker))));
    }

    public static string Restore(string source, CSharpParseOptions? options,
        CancellationToken cancellationToken)
    {
        if (source.IndexOf(Marker, StringComparison.Ordinal) < 0) return source;
        var root = CSharpSyntaxTree.ParseText(source, options, cancellationToken: cancellationToken)
            .GetRoot(cancellationToken);
        return new Rewriter().Visit(root)!.ToFullString();
    }

    private sealed class Rewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitAnonymousObjectMemberDeclarator(AnonymousObjectMemberDeclaratorSyntax node)
        {
            if (node.NameEquals is not { } name || !IsMarked(name)) return base.VisitAnonymousObjectMemberDeclarator(node);
            var result = (AnonymousObjectMemberDeclaratorSyntax)base.VisitAnonymousObjectMemberDeclarator(node)!;
            return node.Expression is IdentifierNameSyntax value && value.Identifier.ValueText == name.Name.Identifier.ValueText
                ? result.WithNameEquals(null)
                : result;
        }

        public override SyntaxNode? VisitArgument(ArgumentSyntax node)
        {
            if (node.NameColon is not { } name || !IsMarked(name)) return base.VisitArgument(node);
            var result = (ArgumentSyntax)base.VisitArgument(node)!;
            return node.Expression is IdentifierNameSyntax value && value.Identifier.ValueText == name.Name.Identifier.ValueText
                ? result.WithNameColon(null)
                : result;
        }

        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia) => IsMarker(trivia) ? default : base.VisitTrivia(trivia);

        private static bool IsMarked(SyntaxNode node) => node.DescendantTrivia().Any(IsMarker);
        private static bool IsMarker(SyntaxTrivia trivia) =>
            trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) && trivia.ToString() == Marker;
    }
}
