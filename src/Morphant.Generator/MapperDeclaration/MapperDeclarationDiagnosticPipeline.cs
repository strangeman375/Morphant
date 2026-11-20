using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.MapperDeclaration;

internal static class MapperDeclarationDiagnosticPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<MapperDeclarationInfo> declarations,
        IncrementalValueProvider<ImmutableArray<MapperContractAnalysis>>
            contractAnalyses)
    {
        var diagnostics = GeneratorStageGuard.Select(
            context,
            declarations.Collect().Combine(contractAnalyses),
            "BuildMapperDeclarationDiagnostics",
            static (source, cancellationToken) =>
                BuildDiagnostics(
                    source.Left,
                    source.Right,
                    cancellationToken),
            ImmutableArray<Diagnostic>.Empty);

        DiagnosticPipeline.Register(
            context,
            diagnostics,
            "MapperDeclarationDiagnostics");
    }

    private static ImmutableArray<Diagnostic> BuildDiagnostics(
        ImmutableArray<MapperDeclarationInfo> declarations,
        ImmutableArray<MapperContractAnalysis> contractAnalyses,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var missingTypeMapper =
            ImmutableArray.CreateBuilder<OrderedDiagnostic>();
        var mapperPartial =
            ImmutableArray.CreateBuilder<OrderedDiagnostic>();
        var containingPartial =
            ImmutableArray.CreateBuilder<OrderedDiagnostic>();
        var fileLocal =
            ImmutableArray.CreateBuilder<OrderedDiagnostic>();
        var exactContracts =
            ImmutableArray.CreateBuilder<OrderedDiagnostic>();
        var unifiableContracts =
            ImmutableArray.CreateBuilder<OrderedDiagnostic>();
        var supportsConflicts =
            ImmutableArray.CreateBuilder<OrderedDiagnostic>();
        var seenMappers = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);
        var seenPartialContainers = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);
        var seenFileLocalTypes = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);

        foreach (var declaration in OrderDeclarations(declarations))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!seenMappers.Add(declaration.MapperType))
            {
                continue;
            }

            if (declaration.HasMissingTypeMapperDiagnostic)
            {
                missingTypeMapper.Add(CreateOrderedDiagnostic(
                    Diagnostic.Create(
                        MapperDeclarationDiagnosticDescriptors
                            .MissingTypeMapperBase,
                        declaration.Attribute.Name.GetLocation(),
                        declaration.MapperDisplayName),
                    declaration));
                continue;
            }

            if (!declaration.DerivesFromTypeMapper)
            {
                continue;
            }

            if (declaration.MapperPartialIssue is { } partialIssue)
            {
                mapperPartial.Add(CreateOrderedDiagnostic(
                    Diagnostic.Create(
                        MapperDeclarationDiagnosticDescriptors
                            .MapperMustBePartial,
                        partialIssue.Identifier.GetLocation(),
                        declaration.MapperDisplayName),
                    declaration));
            }

            foreach (var issue in declaration.ContainingPartialIssues)
            {
                if (!seenPartialContainers.Add(issue.Type))
                {
                    continue;
                }

                containingPartial.Add(CreateOrderedDiagnostic(
                    Diagnostic.Create(
                        MapperDeclarationDiagnosticDescriptors
                            .ContainingTypeMustBePartial,
                        issue.Declaration.Identifier.GetLocation(),
                        issue.DisplayName),
                    declaration));
            }

            foreach (var issue in declaration.FileLocalIssues)
            {
                if (!seenFileLocalTypes.Add(issue.Type))
                {
                    continue;
                }

                fileLocal.Add(CreateOrderedDiagnostic(
                    Diagnostic.Create(
                        MapperDeclarationDiagnosticDescriptors.FileLocalType,
                        issue.Declaration.Modifiers
                            .First(static modifier =>
                                modifier.IsKind(SyntaxKind.FileKeyword))
                            .GetLocation(),
                        issue.DisplayName),
                    declaration));
            }

            if (!declaration.ConflictingSupportsMethods.IsEmpty)
            {
                supportsConflicts.Add(CreateOrderedDiagnostic(
                    Diagnostic.Create(
                        MapperDeclarationDiagnosticDescriptors
                            .SupportsConflict,
                        declaration.ConflictingSupportsMethods[0]
                            .Identifier.GetLocation(),
                        declaration.ConflictingSupportsMethods
                            .Skip(1)
                            .Select(static method =>
                                method.Identifier.GetLocation()),
                        properties: null,
                        declaration.MapperDisplayName),
                    declaration));
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
                var diagnostic = CreateOrderedDiagnostic(
                    Diagnostic.Create(
                        descriptor,
                        GetMapIdentifierLocation(
                            conflict.Registration.Syntax),
                        conflict.InterfaceSyntaxes.Select(static syntax =>
                            syntax.GetLocation()),
                        properties: null,
                        conflict.ContractDisplayName,
                        analysis.Configuration.Declaration
                            .MapperDisplayName),
                    analysis.Configuration.Declaration);

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

        var comparer = DiagnosticSourceOrderComparer.Instance;
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
        ImmutableArray<MapperDeclarationInfo> declarations)
    {
        return declarations
            .OrderBy(declaration =>
                new SyntaxTreeOrdering(
                    declaration.Compilation.SyntaxTrees).GetOrder(
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
        ImmutableArray<OrderedDiagnostic>.Builder source,
        IComparer<OrderedDiagnostic> comparer)
    {
        destination.AddRange(
            source.ToImmutable()
                .Sort(comparer)
                .Select(static diagnostic => diagnostic.Diagnostic));
    }

    private static OrderedDiagnostic CreateOrderedDiagnostic(
        Diagnostic diagnostic,
        MapperDeclarationInfo declaration)
    {
        return new OrderedDiagnostic(
            diagnostic,
            new SyntaxTreeOrdering(
                declaration.Compilation.SyntaxTrees).GetOrderOrDefault(
                diagnostic.Location.SourceTree));
    }

    private sealed class DiagnosticSourceOrderComparer :
        IComparer<OrderedDiagnostic>
    {
        public static DiagnosticSourceOrderComparer Instance { get; } = new();

        private DiagnosticSourceOrderComparer()
        {
        }

        public int Compare(OrderedDiagnostic left, OrderedDiagnostic right)
        {
            var comparison = left.TreeOrder.CompareTo(right.TreeOrder);

            return comparison != 0
                ? comparison
                : left.Diagnostic.Location.SourceSpan.Start.CompareTo(
                    right.Diagnostic.Location.SourceSpan.Start);
        }
    }

    private readonly record struct OrderedDiagnostic(
        Diagnostic Diagnostic,
        int TreeOrder);
}
