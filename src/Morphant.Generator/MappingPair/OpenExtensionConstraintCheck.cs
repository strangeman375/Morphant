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
    public static bool CanConstruct(OpenExtensionReceiver receiver, CSharpCompilation compilation, CancellationToken token) =>
        CanConstruct(receiver, compilation, new HashSet<(string, string)>(), token);

    private static bool CanConstruct(OpenExtensionReceiver receiver, CSharpCompilation compilation,
        HashSet<(string, string)> visited, CancellationToken token)
    {
        if (!visited.Add((receiver.Source, receiver.Destination)))
            return false;
        var generic = receiver.Parameters.Length == 0 ? "" : "<" + string.Join(", ", receiver.Parameters) + ">";
        var signature = "private static void Check" + generic + "(" + receiver.Source + " x, " + receiver.Destination + " y)";
        var options = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        var model = Bind("");
        var diagnostics = model.GetDeclarationDiagnostics(cancellationToken: token);
        if (!diagnostics.Any(IsConstraintViolation))
            return true;
        if (receiver.Parameters.Length == 0)
            return false;

        var method = model.SyntaxTree.GetRoot(token).DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var symbol = model.GetDeclaredSymbol(method, token)!;
        var bounds = PairTypeParameterModelBuilder.Build(symbol.Parameters[0].Type, symbol.Parameters[1].Type,
            symbol.TypeParameters, GeneratedTypeNameBuilder.AllocateTypeParameterNames(symbol.TypeParameters), model.Compilation);
        var clauses = string.Concat(bounds.Where(static p => !p.Constraints.IsEmpty)
            .Select(static p => " where " + p.Name + " : " + string.Join(", ", p.Constraints)));
        if (clauses.Length != 0 &&
            !Bind(clauses).GetDeclarationDiagnostics(cancellationToken: token).Any(d => d.Severity == DiagnosticSeverity.Error))
            return true;

        // Some intersections exist only for specific residual arguments. In
        // particular IInvariant<T> can match IInvariant<int> only at T = int.
        var source = symbol.Parameters[0].Type;
        var destination = symbol.Parameters[1].Type;
        foreach (var (from, to) in Specializations(source, model.Compilation, token)
                     .Concat(Specializations(destination, model.Compilation, token)))
        {
            var candidate = MappingTypeIdentityPolicy.SpecializeReceiver(source, destination, from, to);
            if (candidate is not null && candidate.Parameters.Length < receiver.Parameters.Length &&
                CanConstruct(candidate, compilation, visited, token))
                return true;
        }

        return false;

        SemanticModel Bind(string constraints)
        {
            var text = "namespace Morphant.Generated { internal static class __OpenPairConstraintCheck { " +
                       signature + constraints + " { } } }";
            var tree = CSharpSyntaxTree.ParseText(text, options, cancellationToken: token);
            return compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);
        }
    }

    private static IEnumerable<(ITypeSymbol From, ITypeSymbol To)> Specializations(
        ITypeSymbol type, Compilation compilation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (type is IArrayTypeSymbol array)
        {
            foreach (var candidate in Specializations(array.ElementType, compilation, token))
                yield return candidate;
        }

        if (type is not INamedTypeSymbol named)
            yield break;

        if (named.ContainingType is { } container)
        {
            foreach (var candidate in Specializations(container, compilation, token))
                yield return candidate;
        }

        for (var i = 0; i < named.Arity; i++)
        {
            var argument = named.TypeArguments[i];
            foreach (var bound in named.TypeParameters[i].ConstraintTypes)
            {
                var required = MapperTypeSubstitution.Substitute(bound,
                    MapperTypeSubstitution.Build(named.OriginalDefinition, named), compilation);
                foreach (var conversion in ConversionEqualities(argument, required))
                    yield return conversion;
            }
            foreach (var candidate in Specializations(argument, compilation, token))
                yield return candidate;
        }
    }

    private static IEnumerable<(ITypeSymbol From, ITypeSymbol To)> ConversionEqualities(ITypeSymbol from, ITypeSymbol to)
    {
        if (SymbolEqualityComparer.Default.Equals(from, to))
            yield break;

        yield return (from, to);
        if (from is ITypeParameterSymbol)
            yield break;

        var projections = from.AllInterfaces.AsEnumerable().Concat(BaseTypes(from));
        if (to is ITypeParameterSymbol)
        {
            foreach (var projection in projections)
                yield return (projection, to);
        }
        else if (to is INamedTypeSymbol target && target.Arity > 0)
        {
            if (from is INamedTypeSymbol source)
                projections = projections.Prepend(source);

            foreach (var projection in projections.Where(p =>
                         SymbolEqualityComparer.Default.Equals(p.OriginalDefinition, target.OriginalDefinition)))
            {
                yield return (projection, target);
                for (var i = 0; i < target.Arity; i++)
                {
                    var actual = projection.TypeArguments[i];
                    var expected = target.TypeArguments[i];
                    if (target.TypeParameters[i].Variance == VarianceKind.None)
                        yield return (actual, expected);
                    else
                    {
                        var covariant = target.TypeParameters[i].Variance == VarianceKind.Out;
                        foreach (var equality in ConversionEqualities(covariant ? actual : expected, covariant ? expected : actual))
                            yield return equality;
                    }
                }
            }
        }

        static IEnumerable<INamedTypeSymbol> BaseTypes(ITypeSymbol type)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
                yield return current;
        }
    }

    private static bool IsConstraintViolation(Diagnostic d) => d.Severity == DiagnosticSeverity.Error &&
        d.Id is "CS0310" or "CS0311" or "CS0312" or "CS0313" or "CS0314" or "CS0315" or
                "CS0452" or "CS0453" or "CS8377" or "CS9040" or "CS9244";
}
internal sealed record OpenExtensionReceiver(string Source, string Destination, string[] Parameters);
