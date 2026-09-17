using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.TypeMapperGeneration;

// Keep binding identities until helpers and their parameter lists are final.
// Roslyn's scopes then decide which user names actually need a suffix.
internal sealed class TransferredLocalNames
{
    private readonly Dictionary<string, string> _preferred = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reserved = new(StringComparer.Ordinal);
    private readonly Dictionary<ISymbol, string> _bindings = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<ExpressionSyntax, string> _initializers = new();
    private readonly HashSet<string> _escaped = new(StringComparer.Ordinal);
    private int _ordinal;

    public TransferredLocalNames(INamedTypeSymbol mapper, CancellationToken cancellationToken)
    {
        for (var type = mapper; type is not null; type = type.BaseType)
            foreach (var declaration in type.DeclaringSyntaxReferences)
                foreach (var token in declaration.GetSyntax(cancellationToken).DescendantTokens())
                    if (token.IsKind(SyntaxKind.IdentifierToken)) _reserved.Add(token.ValueText);
    }

    public string Allocate(ISymbol binding, string preferred)
    {
        if (_bindings.TryGetValue(binding, out var existing)) return existing;
        var declaration = binding.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        var name = Allocate(preferred, declaration is not null && DeclarationToken(declaration).Text.StartsWith("@", StringComparison.Ordinal));
        _bindings.Add(binding, name);
        return name;
    }

    public string Allocate(ExpressionSyntax initializer, string preferred)
    {
        if (_initializers.TryGetValue(initializer, out var existing)) return existing;
        var declaration = initializer.Ancestors().OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(variable => variable.Identifier.ValueText == preferred);
        var name = Allocate(preferred, declaration?.Identifier.Text.StartsWith("@", StringComparison.Ordinal) == true);
        _initializers.Add(initializer, name);
        return name;
    }

    private string Allocate(string preferred, bool escaped)
    {
        string name;
        do name = "__morphantLocal" + _ordinal++;
        while (!_reserved.Add(name));
        _preferred.Add(name, preferred);
        if (escaped) _escaped.Add(name);
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

        var candidates = declarations.Where(pair => _preferred.ContainsKey(pair.Key.Name))
            .OrderBy(pair => pair.Value.SpanStart).Select(pair => pair.Key).ToArray();
        var names = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        var allocated = declarations.Keys.Where(symbol => !_preferred.ContainsKey(symbol.Name))
            .ToLookup(symbol => symbol.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        bool Visible(ISymbol symbol, int position) => semantic.LookupSymbols(position, name: symbol.Name)
            .Contains(symbol, SymbolEqualityComparer.Default);

        bool Available(ISymbol symbol, string name)
        {
            if (!allocated.TryGetValue(name, out var others)) return true;
            var token = declarations[symbol];
            foreach (var other in others)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var otherToken = declarations[other];
                if (SeparateJoinBindings(token.Parent, otherToken.Parent)) continue;
                // Check both directions: a nested declaration can precede the
                // enclosing declaration, and C# still forbids the collision.
                if (Visible(other, token.SpanStart) || Visible(symbol, otherToken.SpanStart)) return false;
            }
            return true;
        }

        void Allocate(ISymbol symbol, string name)
        {
            names.Add(symbol, name);
            if (!allocated.TryGetValue(name, out var others)) allocated.Add(name, others = []);
            others.Add(symbol);
        }

        // Preserve available user names before allocating suffixes, including
        // names such as source1 which a conflicting source must not take.
        foreach (var symbol in candidates)
            if (Available(symbol, _preferred[symbol.Name]))
                Allocate(symbol, _preferred[symbol.Name]);
        foreach (var symbol in candidates)
        {
            if (names.ContainsKey(symbol)) continue;
            var preferred = _preferred[symbol.Name];
            var suffix = 1;
            while (!Available(symbol, preferred + suffix)) suffix++;
            Allocate(symbol, preferred + suffix);
        }

        var replacements = new Dictionary<SyntaxToken, string>();
        string Spell(ISymbol symbol, string name) =>
            SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ||
            _escaped.Contains(symbol.Name) && name == _preferred[symbol.Name] ? "@" + name : name;
        foreach (var pair in names) replacements.Add(declarations[pair.Key], Spell(pair.Key, pair.Value));
        foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            if (semantic.GetSymbolInfo(identifier, cancellationToken).Symbol is { } symbol &&
                names.TryGetValue(symbol, out var name)) replacements[identifier.Identifier] = Spell(symbol, name);

        var changes = replacements.Select(pair => new TextChange(pair.Key.Span, pair.Value)).ToList();
        // Only names actually taken by a visible local can capture a member.
        // Qualify that reference instead of renaming the user's declaration.
        foreach (var identifier in root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            if (!allocated.TryGetValue(identifier.Identifier.ValueText, out var locals) ||
                !locals.Any(local => names.ContainsKey(local) && Visible(local, identifier.SpanStart))) continue;
            if (identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier ||
                identifier.Parent is MemberBindingExpressionSyntax or QualifiedNameSyntax or AliasQualifiedNameSyntax
                    or NameEqualsSyntax or NameColonSyntax ||
                identifier.Parent is AssignmentExpressionSyntax assignment && assignment.Left == identifier &&
                    assignment.Parent is InitializerExpressionSyntax) continue;
            var symbol = semantic.GetSymbolInfo(identifier, cancellationToken).Symbol;
            if (symbol is not (IMethodSymbol { MethodKind: MethodKind.Ordinary } or IFieldSymbol or IPropertySymbol or IEventSymbol)) continue;
            var receiver = symbol.IsStatic
                ? symbol.ContainingType.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable) + "."
                : "this.";
            changes.Add(new TextChange(new TextSpan(identifier.SpanStart, 0), receiver));
        }
        return tree.GetText(cancellationToken).WithChanges(changes).ToString();
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
