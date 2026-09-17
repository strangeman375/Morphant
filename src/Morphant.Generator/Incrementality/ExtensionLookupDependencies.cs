using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.Incrementality;

internal static class ExtensionLookupDependencies
{
    private static readonly ConditionalWeakTable<CSharpCompilation, Index> Indexes = new();

    public static void Add(CSharpCompilation compilation, INamedTypeSymbol mapperType,
        Action<ITypeSymbol> addDependency, CancellationToken cancellationToken)
    {
        var index = Indexes.GetValue(compilation, static value => new Index(value));
        foreach (var type in index.GetTypes(mapperType.ContainingNamespace, cancellationToken))
            addDependency(type);
    }

    // Symbols and semantic models never outlive the compilation that owns them.
    private sealed class Index(CSharpCompilation compilation)
    {
        private readonly ConcurrentDictionary<INamespaceSymbol, ImmutableArray<ITypeSymbol>> _scopes =
            new(SymbolEqualityComparer.Default);
        private readonly ConcurrentDictionary<INamedTypeSymbol, ImmutableArray<ITypeSymbol>> _types =
            new(SymbolEqualityComparer.Default);
        private ImmutableArray<ITypeSymbol> _globalTypes;

        public IEnumerable<ITypeSymbol> GetTypes(INamespaceSymbol mapperNamespace, CancellationToken cancellationToken)
        {
            // Enclosing namespaces precede the generated file's static imports.
            // Track competitors even when Configure does not currently select them.
            for (var scope = compilation.GetCompilationNamespace(mapperNamespace);
                 scope is not null; scope = scope.ContainingNamespace)
                foreach (var type in InNamespace(scope, cancellationToken))
                    yield return type;

            if (_globalTypes.IsDefault)
                ImmutableInterlocked.InterlockedInitialize(ref _globalTypes, BuildGlobalTypes(cancellationToken));
            foreach (var type in _globalTypes)
                yield return type;
        }

        private ImmutableArray<ITypeSymbol> InNamespace(INamespaceSymbol scope, CancellationToken cancellationToken) =>
            _scopes.GetOrAdd(scope, value => value.GetTypeMembers()
                .Where(type => type.MightContainExtensionMethods)
                .SelectMany(type => InType(type, cancellationToken)).ToImmutableArray());

        private ImmutableArray<ITypeSymbol> InType(INamedTypeSymbol type, CancellationToken cancellationToken) =>
            _types.GetOrAdd(type, value =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = ImmutableArray.CreateBuilder<ITypeSymbol>();
                result.Add(value);
                // Applicability also depends on argument hierarchies and constraints.
                foreach (var method in value.GetMembers().OfType<IMethodSymbol>().Where(method => method.IsExtensionMethod))
                {
                    result.AddRange(method.Parameters.Select(parameter => parameter.Type));
                    result.AddRange(method.TypeParameters);
                }
                return result.ToImmutable();
            });

        private ImmutableArray<ITypeSymbol> BuildGlobalTypes(CancellationToken cancellationToken)
        {
            var result = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var tree in compilation.SyntaxTrees)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var root = (CompilationUnitSyntax)tree.GetRoot(cancellationToken);
                SemanticModel? semanticModel = null;
                foreach (var directive in root.Usings.Where(directive =>
                             directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword) &&
                             directive.Alias is null && directive.Name is not null))
                {
                    semanticModel ??= compilation.GetSemanticModel(tree);
                    switch (semanticModel.GetSymbolInfo(directive.Name!, cancellationToken).Symbol)
                    {
                        case INamespaceSymbol scope:
                            result.UnionWith(InNamespace(scope, cancellationToken));
                            break;
                        case INamedTypeSymbol type:
                            result.UnionWith(InType(type, cancellationToken));
                            break;
                    }
                }
            }
            return result.ToImmutableArray();
        }
    }
}
