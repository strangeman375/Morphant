using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.ConstructionSurface.PairConfiguration;

namespace Morphant.Generator.MappingPair;

internal static class OpenExtensionConstraintCheck
{
    public static bool HasConstraints(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
            return HasConstraints(array.ElementType);
        return type is INamedTypeSymbol named &&
               (named.TypeParameters.Any(static p => p.HasValueTypeConstraint || p.HasReferenceTypeConstraint ||
                    p.HasConstructorConstraint || p.HasUnmanagedTypeConstraint || !p.ConstraintTypes.IsEmpty) ||
                named.ContainingType is { } container && HasConstraints(container) ||
                named.TypeArguments.Any(HasConstraints));
    }

    // The unified receiver contains the constraints from both registrations.
    // Ask the same compiler that binds the DSL whether it can declare it.
    public static bool CanConstruct(string source, string destination, IReadOnlyCollection<string> parameters,
        CSharpCompilation compilation, CancellationToken token)
    {
        var generic = parameters.Count == 0 ? "" : "<" + string.Join(", ", parameters) + ">";
        var signature = "private static void Check" + generic + "(" + source + " x, " + destination + " y)";
        var options = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        var model = Bind("");
        var diagnostics = model.GetDeclarationDiagnostics(cancellationToken: token);
        if (!diagnostics.Any(IsConstraintViolation))
            return true;
        if (parameters.Count == 0)
            return false;

        var method = model.SyntaxTree.GetRoot(token).DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var symbol = model.GetDeclaredSymbol(method, token)!;
        var bounds = PairTypeParameterModelBuilder.Build(symbol.Parameters[0].Type, symbol.Parameters[1].Type,
            symbol.TypeParameters, GeneratedTypeNameBuilder.AllocateTypeParameterNames(symbol.TypeParameters), model.Compilation);
        var clauses = string.Concat(bounds.Where(static p => !p.Constraints.IsEmpty)
            .Select(static p => " where " + p.Name + " : " + string.Join(", ", p.Constraints)));
        if (clauses.Length == 0)
            return false;

        model = Bind(clauses);
        diagnostics = model.GetDeclarationDiagnostics(cancellationToken: token);
        // A concrete bound obtained by substitution (for example, T : string)
        // can have a witness even though C# cannot spell it in a where clause.
        var clauseSyntax = model.SyntaxTree.GetRoot(token).DescendantNodes().OfType<TypeParameterConstraintClauseSyntax>();
        return diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error &&
                   clauseSyntax.Any(clause => clause.Span.Contains(d.Location.SourceSpan))) ||
               !diagnostics.Any(IsConstraintViolation);

        SemanticModel Bind(string constraints)
        {
            var text = "namespace Morphant.Generated { internal static class __OpenPairConstraintCheck { " +
                       signature + constraints + " { } } }";
            var tree = CSharpSyntaxTree.ParseText(text, options, cancellationToken: token);
            return compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);
        }
    }

    private static bool IsConstraintViolation(Diagnostic d) => d.Severity == DiagnosticSeverity.Error &&
        d.Id is "CS0310" or "CS0311" or "CS0312" or "CS0313" or "CS0314" or "CS0315" or
                "CS0452" or "CS0453" or "CS8377" or "CS9040" or "CS9244";
}