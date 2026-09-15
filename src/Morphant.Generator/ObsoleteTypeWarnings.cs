using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator;

internal readonly record struct ObsoleteTypeWarning(string TypeName, string DiagnosticId);

internal static class ObsoleteTypeWarnings
{
    public static ImmutableArray<ObsoleteTypeWarning> CollectDeclarations(params ITypeSymbol[] types) =>
        Collect(types.SelectMany(DeclarationTypes));

    private static IEnumerable<ITypeSymbol> DeclarationTypes(ITypeSymbol type)
    {
        yield return type;
        for (var named = type as INamedTypeSymbol; named is not null; named = named.BaseType)
        {
            foreach (var member in named.GetMembers())
            {
                switch (member)
                {
                    case IPropertySymbol property:
                        yield return property.Type;
                        break;
                    case IFieldSymbol field:
                        yield return field.Type;
                        break;
                    case IMethodSymbol method:
                        yield return method.ReturnType;
                        foreach (var parameter in method.Parameters) yield return parameter.Type;
                        break;
                }
            }
        }
    }

    public static ImmutableArray<ObsoleteTypeWarning> Collect(params ITypeSymbol[] types) =>
        Collect((IEnumerable<ITypeSymbol>)types);

    public static ImmutableArray<ObsoleteTypeWarning> Collect(IEnumerable<ITypeSymbol> types)
    {
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var warnings = new HashSet<ObsoleteTypeWarning>();

        void Visit(ITypeSymbol type)
        {
            if (!visited.Add(type)) return;
            foreach (var attribute in type.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != "System.ObsoleteAttribute" ||
                    attribute.ConstructorArguments is { Length: > 1 } arguments &&
                    arguments[1].Value is true)
                    continue;

                var id = attribute.NamedArguments.FirstOrDefault(argument =>
                    argument.Key == "DiagnosticId").Value.Value as string;
                if (string.IsNullOrEmpty(id))
                    id = attribute.ConstructorArguments.Length == 0 ||
                         attribute.ConstructorArguments[0].Value is null ? "CS0612" : "CS0618";

                // C# warning directives require an identifier. Keep an unusual
                // custom diagnostic visible rather than emitting invalid syntax.
                if (SyntaxFacts.IsValidIdentifier(id!))
                    warnings.Add(new ObsoleteTypeWarning(type.Name, id!));
            }

            switch (type)
            {
                case INamedTypeSymbol named:
                    if (named.ContainingType is { } containing) Visit(containing);
                    foreach (var argument in named.TypeArguments) Visit(argument);
                    break;
                case IArrayTypeSymbol array:
                    Visit(array.ElementType);
                    break;
                case IPointerTypeSymbol pointer:
                    Visit(pointer.PointedAtType);
                    break;
                case ITypeParameterSymbol parameter:
                    foreach (var constraint in parameter.ConstraintTypes) Visit(constraint);
                    break;
            }
        }

        foreach (var type in types) Visit(type);
        return warnings.OrderBy(warning => warning.TypeName, StringComparer.Ordinal)
            .ThenBy(warning => warning.DiagnosticId, StringComparer.Ordinal).ToImmutableArray();
    }

    public static string Suppress(
        string source,
        ImmutableArray<ObsoleteTypeWarning> warnings,
        bool declarationSurface = false)
    {
        if (warnings.IsDefaultOrEmpty) return source;
        var text = SourceText.From(source);
        var root = CSharpSyntaxTree.ParseText(text).GetRoot();
        var directives = root.DescendantTrivia(descendIntoTrivia: true)
            .Select(trivia => trivia.GetStructure())
            .OfType<PragmaWarningDirectiveTriviaSyntax>().Where(directive => directive.IsActive)
            .ToArray();
        var changes = new List<TextChange>();

        void SuppressRange(SyntaxNode node, int end)
        {
            if (node.AncestorsAndSelf().OfType<MemberDeclarationSyntax>()
                .Any(HasObsoleteAttribute)) return;

            var ids = warnings.Where(warning => node.DescendantNodesAndSelf()
                    .OfType<SimpleNameSyntax>().Any(name => name.SpanStart < end &&
                        name.Identifier.ValueText == warning.TypeName))
                .Select(warning => warning.DiagnosticId).Distinct(StringComparer.Ordinal)
                .Where(id => !IsSuppressed(directives, node.SpanStart, id))
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (ids.Length == 0) return;

            var first = node.GetLeadingTrivia().FirstOrDefault(trivia =>
                trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
            var start = text.Lines.GetLineFromPosition(
                first.RawKind == 0 ? node.SpanStart : first.FullSpan.Start).Start;
            var indentation = new string(source.Skip(start).TakeWhile(character =>
                character is ' ' or '\t').ToArray());
            var endLine = text.Lines.GetLineFromPosition(end);
            var wholeDeclaration = end == node.Span.End;
            var atLineStart = wholeDeclaration ||
                string.IsNullOrWhiteSpace(source.Substring(endLine.Start, end - endLine.Start));
            if (wholeDeclaration) end = endLine.EndIncludingLineBreak;
            else if (atLineStart) end = endLine.Start;

            changes.Add(new TextChange(new TextSpan(start, 0),
                indentation + "#pragma warning disable " + string.Join(", ", ids) + "\r\n"));
            changes.Add(new TextChange(new TextSpan(end, 0),
                (atLineStart ? string.Empty : "\r\n") + indentation +
                "#pragma warning restore " + string.Join(", ", ids) + "\r\n" +
                (atLineStart ? string.Empty : indentation)));
        }

        if (declarationSurface)
        {
            // These files contain only DSL declarations and throwing stubs.
            // Their attributes still diagnose calls in the user's source file.
            foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>()
                         .Where(type => !type.Ancestors().OfType<TypeDeclarationSyntax>().Any()))
                SuppressRange(declaration, declaration.Span.End);
        }
        else
        {
            foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                SuppressRange(declaration, declaration.OpenBraceToken.SpanStart);
            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (method.Identifier.ValueText == "Supports" &&
                    method.Modifiers.Any(SyntaxKind.OverrideKeyword))
                    SuppressRange(method, method.Span.End);
                else if (method.Body is { } body)
                    SuppressRange(method, body.OpenBraceToken.SpanStart);
                else if (method.ExpressionBody is { } expression)
                    SuppressRange(method, expression.ArrowToken.SpanStart);
            }
        }

        return changes.Count == 0 ? source : text.WithChanges(changes).ToString();
    }

    private static bool HasObsoleteAttribute(MemberDeclarationSyntax declaration) =>
        declaration.AttributeLists.SelectMany(list => list.Attributes).Any(attribute =>
            attribute.Name.ToString() is "global::System.ObsoleteAttribute" or
                "System.ObsoleteAttribute" or "ObsoleteAttribute" or "Obsolete");

    private static bool IsSuppressed(
        IEnumerable<PragmaWarningDirectiveTriviaSyntax> directives, int position, string id)
    {
        var all = false;
        bool? specific = null;
        foreach (var directive in directives.TakeWhile(directive => directive.SpanStart < position))
        {
            var disable = directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword);
            if (directive.ErrorCodes.Count == 0)
            {
                all = disable;
                specific = null;
            }
            else if (directive.ErrorCodes.Any(code => code.ToString() == id ||
                         int.TryParse(code.ToString(), out var number) && id == "CS" + number.ToString("D4")))
                specific = disable;
        }
        return specific ?? all;
    }
}
