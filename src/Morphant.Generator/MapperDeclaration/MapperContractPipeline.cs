using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.MapperDeclaration;

internal static class MapperContractPipeline
{
    public static IncrementalValuesProvider<MapperContractAnalysis> Build(
        IncrementalValuesProvider<MapperPairConfigurationModel> configurations,
        IncrementalValueProvider<CompilationContext> compilationContext)
    {
        return configurations
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                BuildAnalysis(
                    source.Left,
                    source.Right,
                    cancellationToken))
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildMapperContractAnalyses);
    }

    private static MapperContractAnalysis BuildAnalysis(
        MapperPairConfigurationModel configuration,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!configuration.Declaration.CanGenerateExecutableArtifact ||
            context.KnownSymbols is not { } knownSymbols)
        {
            return new MapperContractAnalysis(configuration, []);
        }

        var interfaceGraphs = FindDirectInterfaceGraphs(
            configuration.Declaration.MapperType,
            knownSymbols.TypeMapperInterface,
            context.Compilation,
            cancellationToken);
        var conflicts =
            ImmutableArray.CreateBuilder<MapperContractConflict>();

        foreach (var pair in EnumeratePairs(configuration.MappingPairs))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var exactSyntaxes = interfaceGraphs
                .Where(graph => graph.Contracts.Any(contract =>
                    IsExactContract(contract, pair)))
                .Select(static graph => graph.Syntax)
                .ToImmutableArray();

            if (!exactSyntaxes.IsEmpty)
            {
                conflicts.Add(CreateConflict(
                    MapperContractConflictKind.Exact,
                    pair,
                    exactSyntaxes));
                continue;
            }

            var unifiableSyntaxes = interfaceGraphs
                .Where(graph => graph.Contracts.Any(contract =>
                    IsUnifiableContract(contract, pair)))
                .Select(static graph => graph.Syntax)
                .ToImmutableArray();

            if (!unifiableSyntaxes.IsEmpty)
            {
                conflicts.Add(CreateConflict(
                    MapperContractConflictKind.Unifiable,
                    pair,
                    unifiableSyntaxes));
            }
        }

        return new MapperContractAnalysis(
            configuration,
            conflicts.ToImmutable());
    }

    private static ImmutableArray<DirectInterfaceGraph>
        FindDirectInterfaceGraphs(
            INamedTypeSymbol mapperType,
            INamedTypeSymbol typeMapperInterface,
            Compilation compilation,
            CancellationToken cancellationToken)
    {
        var syntaxTreeOrder = compilation.SyntaxTrees
            .Select((tree, index) => (tree, index))
            .ToDictionary(
                static item => item.tree,
                static item => item.index);
        var result = ImmutableArray.CreateBuilder<DirectInterfaceGraph>();

        foreach (var declaration in mapperType.DeclaringSyntaxReferences
                     .Select(reference =>
                         reference.GetSyntax(cancellationToken))
                     .OfType<ClassDeclarationSyntax>()
                     .OrderBy(syntax => syntaxTreeOrder[syntax.SyntaxTree])
                     .ThenBy(static syntax => syntax.SpanStart))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (declaration.BaseList is null)
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(
                declaration.SyntaxTree);

            foreach (var baseType in declaration.BaseList.Types)
            {
                if (semanticModel.GetTypeInfo(
                        baseType.Type,
                        cancellationToken).Type is not
                        INamedTypeSymbol
                        {
                            TypeKind: TypeKind.Interface
                        } directInterface)
                {
                    continue;
                }

                var contracts = EnumerateInterfaceGraph(directInterface)
                    .Where(candidate =>
                        SymbolEqualityComparer.Default.Equals(
                            candidate.OriginalDefinition,
                            typeMapperInterface.OriginalDefinition))
                    .ToImmutableArray();

                if (!contracts.IsEmpty)
                {
                    result.Add(new DirectInterfaceGraph(
                        baseType.Type,
                        contracts));
                }
            }
        }

        return result.ToImmutable();
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateInterfaceGraph(
        INamedTypeSymbol directInterface)
    {
        yield return directInterface;

        foreach (var inheritedInterface in directInterface.AllInterfaces)
        {
            yield return inheritedInterface;
        }
    }

    private static IEnumerable<RegisteredPair> EnumeratePairs(
        MapperMappingPairModel model)
    {
        foreach (var pair in model.Pairs)
        {
            yield return new RegisteredPair(
                pair.Registration,
                pair.Identity,
                pair.SourceType,
                pair.DestinationType);
        }

        foreach (var pair in model.UnsupportedPairs)
        {
            yield return new RegisteredPair(
                pair.Registration,
                pair.Identity,
                pair.SourceType,
                pair.DestinationType);
        }
    }

    private static bool IsExactContract(
        INamedTypeSymbol contract,
        RegisteredPair pair)
    {
        return MappingTypeIdentityPolicy.AreEquivalent(
                   contract.TypeArguments[0],
                   pair.SourceType) &&
               MappingTypeIdentityPolicy.AreEquivalent(
                   contract.TypeArguments[1],
                   pair.DestinationType);
    }

    private static bool IsUnifiableContract(
        INamedTypeSymbol contract,
        RegisteredPair pair)
    {
        return MappingTypeIdentityPolicy.CanPairsUnify(
            contract.TypeArguments[0],
            contract.TypeArguments[1],
            pair.SourceType,
            pair.DestinationType);
    }

    private static MapperContractConflict CreateConflict(
        MapperContractConflictKind kind,
        RegisteredPair pair,
        ImmutableArray<TypeSyntax> interfaceSyntaxes)
    {
        return new MapperContractConflict(
            kind,
            pair.Registration,
            pair.Identity,
            MapperContractDisplay.Create(
                pair.SourceType,
                pair.DestinationType),
            interfaceSyntaxes);
    }

    private readonly record struct DirectInterfaceGraph(
        TypeSyntax Syntax,
        ImmutableArray<INamedTypeSymbol> Contracts);

    private readonly record struct RegisteredPair(
        MappingPairRegistrationModel Registration,
        MappingPairIdentity Identity,
        ITypeSymbol SourceType,
        ITypeSymbol DestinationType);
}
