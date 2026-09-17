using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.Incrementality;

internal static class ExtensionLookupDependencies
{
    public static void Add(
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        Action<ITypeSymbol> addDependency,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        // Transferred calls are rebound in the generated file, where the
        // mapper's enclosing namespaces precede its file-level static imports.
        // A new competing extension can change that binding without changing
        // any operation in the original Configure method.
        for (var scope = compilation.GetCompilationNamespace(mapperType.ContainingNamespace);
             scope is not null;
             scope = scope.ContainingNamespace)
        {
            AddNamespace(scope);
        }

        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = (CompilationUnitSyntax)tree.GetRoot(cancellationToken);
            var globalUsings = root.Usings.Where(directive =>
                directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword) &&
                directive.Alias is null && directive.Name is not null);
            SemanticModel? semanticModel = null;
            foreach (var directive in globalUsings)
            {
                semanticModel ??= compilation.GetSemanticModel(tree);
                switch (semanticModel.GetSymbolInfo(directive.Name!, cancellationToken).Symbol)
                {
                    case INamespaceSymbol scope:
                        AddNamespace(scope);
                        break;
                    case INamedTypeSymbol type:
                        AddType(type);
                        break;
                }
            }
        }

        void AddNamespace(INamespaceSymbol scope)
        {
            if (!visited.Add(scope)) return;
            foreach (var type in scope.GetTypeMembers())
                if (type.MightContainExtensionMethods)
                    AddType(type);
        }

        void AddType(INamedTypeSymbol type)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(type)) return;
            addDependency(type);
            // Overload applicability also depends on receiver/argument
            // hierarchies and generic constraints outside the extension class.
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>()
                         .Where(method => method.IsExtensionMethod))
            {
                foreach (var parameter in method.Parameters)
                    addDependency(parameter.Type);
                foreach (var parameter in method.TypeParameters)
                    addDependency(parameter);
            }
        }
    }
}
