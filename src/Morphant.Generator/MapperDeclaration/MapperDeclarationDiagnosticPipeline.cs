using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.MapperDeclaration;

internal static class MapperDeclarationDiagnosticPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValuesProvider<MapperDeclarationInfo> declarations,
        IncrementalValueProvider<ImmutableArray<MapperContractAnalysis>>
            contractAnalyses)
    {
        var diagnostics = declarations
            .Collect()
            .Combine(contractAnalyses)
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                BuildDiagnostics(
                    source.Left.Left,
                    source.Left.Right,
                    source.Right,
                    cancellationToken));

        DiagnosticPipeline.Register(context, diagnostics);
    }

    private static ImmutableArray<Diagnostic> BuildDiagnostics(
        ImmutableArray<MapperDeclarationInfo> declarations,
        ImmutableArray<MapperContractAnalysis> contractAnalyses,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var missingTypeMapper = ImmutableArray.CreateBuilder<Diagnostic>();
        var mapperPartial = ImmutableArray.CreateBuilder<Diagnostic>();
        var containingPartial = ImmutableArray.CreateBuilder<Diagnostic>();
        var fileLocal = ImmutableArray.CreateBuilder<Diagnostic>();
        var exactContracts = ImmutableArray.CreateBuilder<Diagnostic>();
        var unifiableContracts = ImmutableArray.CreateBuilder<Diagnostic>();
        var supportsConflicts = ImmutableArray.CreateBuilder<Diagnostic>();
        var seenMappers = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);
        var seenPartialContainers = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);
        var seenFileLocalTypes = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);

        foreach (var declaration in OrderDeclarations(
                     declarations,
                     context.SyntaxTrees))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!seenMappers.Add(declaration.MapperType))
            {
                continue;
            }

            if (declaration.HasMissingTypeMapperDiagnostic)
            {
                missingTypeMapper.Add(Diagnostic.Create(
                    MapperDeclarationDiagnosticDescriptors
                        .MissingTypeMapperBase,
                    declaration.Attribute.Name.GetLocation(),
                    declaration.MapperDisplayName));
                continue;
            }

            if (!declaration.DerivesFromTypeMapper)
            {
                continue;
            }

            if (declaration.MapperPartialIssue is { } partialIssue)
            {
                mapperPartial.Add(Diagnostic.Create(
                    MapperDeclarationDiagnosticDescriptors.MapperMustBePartial,
                    partialIssue.Identifier.GetLocation(),
                    declaration.MapperDisplayName));
            }

            foreach (var issue in declaration.ContainingPartialIssues)
            {
                if (!seenPartialContainers.Add(issue.Type))
                {
                    continue;
                }

                containingPartial.Add(Diagnostic.Create(
                    MapperDeclarationDiagnosticDescriptors
                        .ContainingTypeMustBePartial,
                    issue.Declaration.Identifier.GetLocation(),
                    issue.DisplayName));
            }

            foreach (var issue in declaration.FileLocalIssues)
            {
                if (!seenFileLocalTypes.Add(issue.Type))
                {
                    continue;
                }

                fileLocal.Add(Diagnostic.Create(
                    MapperDeclarationDiagnosticDescriptors.FileLocalType,
                    issue.Declaration.Modifiers
                        .First(static modifier =>
                            modifier.IsKind(SyntaxKind.FileKeyword))
                        .GetLocation(),
                    issue.DisplayName));
            }

            if (!declaration.ConflictingSupportsMethods.IsEmpty)
            {
                supportsConflicts.Add(Diagnostic.Create(
                    MapperDeclarationDiagnosticDescriptors.SupportsConflict,
                    declaration.ConflictingSupportsMethods[0]
                        .Identifier.GetLocation(),
                    declaration.ConflictingSupportsMethods
                        .Skip(1)
                        .Select(static method =>
                            method.Identifier.GetLocation()),
                    properties: null,
                    declaration.MapperDisplayName));
            }
        }

        var seenPairConflicts = new HashSet<string>(StringComparer.Ordinal);

        foreach (var analysis in contractAnalyses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var conflict in analysis.Conflicts)
            {
                var identity = analysis.Configuration.Declaration.MapperIdentity +
                               "|" + conflict.PairIdentity.Source.Key +
                               "|" + conflict.PairIdentity.Destination.Key;

                if (!seenPairConflicts.Add(identity))
                {
                    continue;
                }

                var descriptor = conflict.Kind ==
                        MapperContractConflictKind.Exact
                    ? MapperDeclarationDiagnosticDescriptors.ExactContract
                    : MapperDeclarationDiagnosticDescriptors.UnifiableContract;
                var diagnostic = Diagnostic.Create(
                    descriptor,
                    GetMapIdentifierLocation(conflict.Registration.Syntax),
                    conflict.InterfaceSyntaxes.Select(static syntax =>
                        syntax.GetLocation()),
                    properties: null,
                    conflict.ContractDisplayName,
                    analysis.Configuration.Declaration.MapperDisplayName);

                if (conflict.Kind == MapperContractConflictKind.Exact)
                {
                    exactContracts.Add(diagnostic);
                }
                else
                {
                    unifiableContracts.Add(diagnostic);
                }
            }
        }

        var comparer = new DiagnosticSourceOrderComparer(
            context.SyntaxTrees);
        var result = ImmutableArray.CreateBuilder<Diagnostic>(
            missingTypeMapper.Count +
            mapperPartial.Count +
            containingPartial.Count +
            fileLocal.Count +
            exactContracts.Count +
            unifiableContracts.Count +
            supportsConflicts.Count);

        AddOrdered(result, missingTypeMapper, comparer);
        AddOrdered(result, mapperPartial, comparer);
        AddOrdered(result, containingPartial, comparer);
        AddOrdered(result, fileLocal, comparer);
        AddOrdered(result, exactContracts, comparer);
        AddOrdered(result, unifiableContracts, comparer);
        AddOrdered(result, supportsConflicts, comparer);

        return result.ToImmutable();
    }

    private static IEnumerable<MapperDeclarationInfo> OrderDeclarations(
        ImmutableArray<MapperDeclarationInfo> declarations,
        SyntaxTreeOrdering syntaxTrees)
    {
        return declarations
            .OrderBy(declaration =>
                syntaxTrees.GetOrder(
                    declaration.AttributedDeclaration.SyntaxTree))
            .ThenBy(static declaration =>
                declaration.AttributedDeclaration.SpanStart);
    }

    private static Location GetMapIdentifierLocation(
        InvocationExpressionSyntax invocation)
    {
        var name = invocation.Expression switch
        {
            SimpleNameSyntax simpleName => simpleName,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
            _ => invocation.Expression
                .DescendantNodesAndSelf()
                .OfType<SimpleNameSyntax>()
                .First(simpleName =>
                    simpleName.Identifier.ValueText == "Map")
        };

        return name.Identifier.GetLocation();
    }

    private static void AddOrdered(
        ImmutableArray<Diagnostic>.Builder destination,
        ImmutableArray<Diagnostic>.Builder source,
        IComparer<Diagnostic> comparer)
    {
        destination.AddRange(source.ToImmutable().Sort(comparer));
    }

    private sealed class DiagnosticSourceOrderComparer : IComparer<Diagnostic>
    {
        private readonly SyntaxTreeOrdering _syntaxTrees;

        public DiagnosticSourceOrderComparer(SyntaxTreeOrdering syntaxTrees)
        {
            _syntaxTrees = syntaxTrees;
        }

        public int Compare(Diagnostic? left, Diagnostic? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            var leftTreeIndex = GetTreeIndex(left.Location.SourceTree);
            var rightTreeIndex = GetTreeIndex(right.Location.SourceTree);
            var comparison = leftTreeIndex.CompareTo(rightTreeIndex);

            return comparison != 0
                ? comparison
                : left.Location.SourceSpan.Start.CompareTo(
                    right.Location.SourceSpan.Start);
        }

        private int GetTreeIndex(SyntaxTree? syntaxTree)
        {
            return _syntaxTrees.GetOrderOrDefault(syntaxTree);
        }
    }
}
