using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class IncludedPatternLocalNameRewriter
{
    private const string PlaceholderPrefix =
        "__morphantIncludedScope";

    public static string Rewrite(string source)
    {
        if (source.IndexOf(
                PlaceholderPrefix,
                StringComparison.Ordinal) < 0)
        {
            return source;
        }

        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        return new MethodRewriter().Visit(root)!.ToFullString();
    }

    private static bool TryGetPreferredName(
        string placeholder,
        out string preferredName)
    {
        var pathMarker = placeholder.IndexOf(
            "_Path",
            PlaceholderPrefix.Length,
            StringComparison.Ordinal);

        if (pathMarker < 0)
        {
            preferredName = string.Empty;
            return false;
        }

        var nameSeparator = placeholder.IndexOf(
            '_',
            pathMarker + "_Path".Length);

        if (nameSeparator < 0 ||
            nameSeparator == placeholder.Length - 1)
        {
            preferredName = string.Empty;
            return false;
        }

        var segmentName = placeholder.Substring(nameSeparator + 1);
        preferredName = char.ToLowerInvariant(segmentName[0]) +
                        segmentName.Substring(1);

        if (preferredName == "_" ||
            !(SyntaxFacts.IsValidIdentifier(preferredName) ||
              SyntaxFacts.GetKeywordKind(preferredName) != SyntaxKind.None ||
              SyntaxFacts.GetContextualKeywordKind(preferredName) !=
                  SyntaxKind.None))
        {
            preferredName = "value";
        }

        return true;
    }

    private static string AllocateName(
        string preferredName,
        HashSet<string> usedNames)
    {
        if (usedNames.Add(preferredName))
        {
            return preferredName;
        }

        for (var suffix = 1;; suffix++)
        {
            var candidate = preferredName +
                suffix.ToString(CultureInfo.InvariantCulture);

            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string Identifier(string value) =>
        SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
        SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value
            : value;

    private sealed class MethodRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitMethodDeclaration(
            MethodDeclarationSyntax node)
        {
            var placeholders = BuildPlaceholders(node);

            if (placeholders.Count == 0)
            {
                return node;
            }

            var usedNames = BuildUsedNames(node);

            var replacements = new Dictionary<string, string>(
                StringComparer.Ordinal);

            foreach (var placeholder in placeholders)
            {
                if (!TryGetPreferredName(
                        placeholder,
                        out var preferredName))
                {
                    continue;
                }

                replacements.Add(
                    placeholder,
                    AllocateName(preferredName, usedNames));
            }

            return replacements.Count == 0
                ? node
                : new IdentifierRewriter(replacements).Visit(node);
        }

        private static IReadOnlyList<string> BuildPlaceholders(
            MethodDeclarationSyntax method)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var designation in method.DescendantNodes()
                         .OfType<SingleVariableDesignationSyntax>())
            {
                var name = designation.Identifier.ValueText;

                if (name.StartsWith(
                        PlaceholderPrefix,
                        StringComparison.Ordinal) &&
                    seen.Add(name))
                {
                    result.Add(name);
                }
            }

            return result;
        }

        private static HashSet<string> BuildUsedNames(
            MethodDeclarationSyntax method)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);

            foreach (var node in method.DescendantNodes())
            {
                var name = node switch
                {
                    VariableDeclaratorSyntax variable =>
                        variable.Identifier.ValueText,
                    SingleVariableDesignationSyntax designation
                        when !designation.Identifier.ValueText.StartsWith(
                            PlaceholderPrefix,
                            StringComparison.Ordinal) =>
                        designation.Identifier.ValueText,
                    ParameterSyntax parameter =>
                        parameter.Identifier.ValueText,
                    TypeParameterSyntax typeParameter =>
                        typeParameter.Identifier.ValueText,
                    ForEachStatementSyntax forEach =>
                        forEach.Identifier.ValueText,
                    CatchDeclarationSyntax catchDeclaration
                        when catchDeclaration.Identifier.RawKind != 0 =>
                        catchDeclaration.Identifier.ValueText,
                    LocalFunctionStatementSyntax localFunction =>
                        localFunction.Identifier.ValueText,
                    FromClauseSyntax from => from.Identifier.ValueText,
                    LetClauseSyntax let => let.Identifier.ValueText,
                    JoinClauseSyntax join => join.Identifier.ValueText,
                    JoinIntoClauseSyntax joinInto =>
                        joinInto.Identifier.ValueText,
                    QueryContinuationSyntax continuation =>
                        continuation.Identifier.ValueText,
                    _ => null
                };

                if (name is not null)
                {
                    result.Add(name);
                }
            }

            foreach (var type in method.Ancestors()
                         .OfType<TypeDeclarationSyntax>())
            {
                if (type.TypeParameterList is not { } typeParameters)
                {
                    continue;
                }

                foreach (var typeParameter in typeParameters.Parameters)
                {
                    result.Add(typeParameter.Identifier.ValueText);
                }
            }

            return result;
        }
    }

    private sealed class IdentifierRewriter(
        IReadOnlyDictionary<string, string> replacements)
        : CSharpSyntaxRewriter
    {
        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken) ||
                !replacements.TryGetValue(
                    token.ValueText,
                    out var replacement))
            {
                return token;
            }

            return SyntaxFactory.Identifier(
                token.LeadingTrivia,
                SyntaxKind.IdentifierToken,
                Identifier(replacement),
                replacement,
                token.TrailingTrivia);
        }
    }
}
