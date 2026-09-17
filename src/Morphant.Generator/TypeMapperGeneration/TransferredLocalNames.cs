using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

// Keep binding identities until helpers and their parameter lists are final.
// Roslyn's scopes then decide which user names actually need a suffix.
internal sealed class TransferredLocalNames
{
    private readonly Dictionary<string, string> _preferred = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reserved = new(StringComparer.Ordinal);
    private readonly Dictionary<object, string> _bindings = new();
    private int _ordinal;

    public TransferredLocalNames(INamedTypeSymbol mapper, CancellationToken cancellationToken)
    {
        for (var type = mapper; type is not null; type = type.BaseType)
            foreach (var declaration in type.DeclaringSyntaxReferences)
                foreach (var token in declaration.GetSyntax(cancellationToken).DescendantTokens())
                    if (token.IsKind(SyntaxKind.IdentifierToken)) _reserved.Add(token.ValueText);
    }

    public string Allocate(object binding, string preferred)
    {
        if (_bindings.TryGetValue(binding, out var existing)) return existing;
        string name;
        do name = "__morphantLocal" + _ordinal++;
        while (!_reserved.Add(name));
        _preferred.Add(name, preferred);
        _bindings.Add(binding, name);
        return name;
    }

    public string Restore(string source, CSharpCompilation compilation,
        CSharpParseOptions? options, CancellationToken cancellationToken)
    {
        if (_preferred.Count == 0) return source;
        var tree = CSharpSyntaxTree.ParseText(source, options, cancellationToken: cancellationToken);
        var root = tree.GetRoot(cancellationToken);
        var semantic = compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);
        var declarations = new Dictionary<ISymbol, SyntaxToken>(SymbolEqualityComparer.Default);
        foreach (var node in root.DescendantNodes())
        {
            var token = DeclarationToken(node);
            if (token.RawKind != 0 && semantic.GetDeclaredSymbol(node, cancellationToken) is { } symbol)
                declarations[symbol] = token;
        }

        var candidates = declarations.Keys.Where(symbol => _preferred.ContainsKey(symbol.Name)).ToArray();
        var conflicts = candidates.ToDictionary(symbol => symbol,
            _ => new HashSet<ISymbol>(SymbolEqualityComparer.Default), SymbolEqualityComparer.Default);
        foreach (var pair in declarations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var visible in semantic.LookupSymbols(pair.Value.SpanStart))
            {
                if (SymbolEqualityComparer.Default.Equals(pair.Key, visible) ||
                    !declarations.TryGetValue(visible, out var visibleToken) ||
                    SeparateJoinBindings(pair.Value.Parent, visibleToken.Parent)) continue;
                if (conflicts.TryGetValue(pair.Key, out var own)) own.Add(visible);
                if (conflicts.TryGetValue(visible, out var other)) other.Add(pair.Key);
            }
        }

        var names = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        bool Available(ISymbol symbol, string name) => !conflicts[symbol].Any(other =>
            StringComparer.Ordinal.Equals(name,
                names.TryGetValue(other, out var allocated) ? allocated :
                _preferred.ContainsKey(other.Name) ? null : other.Name));

        // Preserve available user names before allocating suffixes, including
        // names such as source1 which a conflicting source must not take.
        foreach (var symbol in candidates)
            if (Available(symbol, _preferred[symbol.Name]))
                names.Add(symbol, _preferred[symbol.Name]);
        foreach (var symbol in candidates)
        {
            if (names.ContainsKey(symbol)) continue;
            var preferred = _preferred[symbol.Name];
            var suffix = 1;
            while (!Available(symbol, preferred + suffix)) suffix++;
            names.Add(symbol, preferred + suffix);
        }

        var replacements = new Dictionary<SyntaxToken, string>();
        foreach (var pair in names) replacements.Add(declarations[pair.Key], pair.Value);
        foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            if (semantic.GetSymbolInfo(identifier, cancellationToken).Symbol is { } symbol &&
                names.TryGetValue(symbol, out var name)) replacements[identifier.Identifier] = name;

        return root.ReplaceTokens(replacements.Keys, (token, _) =>
        {
            var name = replacements[token];
            var text = SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ||
                SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
            return SyntaxFactory.Identifier(token.LeadingTrivia, SyntaxKind.IdentifierToken,
                text, name, token.TrailingTrivia);
        }).ToFullString();
    }

    private static bool SeparateJoinBindings(SyntaxNode? first, SyntaxNode? second) =>
        first is JoinIntoClauseSyntax into && ReferenceEquals(into.Parent, second) ||
        second is JoinIntoClauseSyntax other && ReferenceEquals(other.Parent, first);

    private static SyntaxToken DeclarationToken(SyntaxNode node) => node switch
    {
        VariableDeclaratorSyntax variable when variable.Parent?.Parent is LocalDeclarationStatementSyntax
            or ForStatementSyntax or UsingStatementSyntax or FixedStatementSyntax => variable.Identifier,
        SingleVariableDesignationSyntax designation => designation.Identifier,
        ParameterSyntax parameter => parameter.Identifier,
        TypeParameterSyntax parameter when parameter.Parent?.Parent is MethodDeclarationSyntax
            or LocalFunctionStatementSyntax => parameter.Identifier,
        ForEachStatementSyntax statement => statement.Identifier,
        CatchDeclarationSyntax declaration => declaration.Identifier,
        LocalFunctionStatementSyntax function => function.Identifier,
        FromClauseSyntax clause => clause.Identifier,
        LetClauseSyntax clause => clause.Identifier,
        JoinClauseSyntax clause => clause.Identifier,
        JoinIntoClauseSyntax clause => clause.Identifier,
        QueryContinuationSyntax continuation => continuation.Identifier,
        _ => default
    };
}
