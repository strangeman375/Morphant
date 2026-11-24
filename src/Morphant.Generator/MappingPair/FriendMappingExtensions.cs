using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class FriendMappingExtensions
{
    private static readonly ConditionalWeakTable<Compilation, ImmutableDictionary<IMethodSymbol, MappingExtensionReceiver>> Indexes = new();

    public static VisibleFriendExtensions Find(Compilation compilation, ITypeSymbol source, ITypeSymbol destination,
        SyntaxNode registration, CancellationToken cancellationToken)
    {
        var index = Indexes.GetValue(compilation, BuildIndex);
        if (index.IsEmpty) return default;

        var pairKey = MappingTypeIdentityPolicy.CreateAlphaEquivalentPairKey(source, destination);
        var receiver = compilation.GetTypeByMetadataName(MetadataNames.PairMapperBuilder)!.Construct(source, destination);
        cancellationToken.ThrowIfCancellationRequested();
        var visible = compilation.GetSemanticModel(registration.SyntaxTree).LookupSymbols(
            registration.SpanStart, receiver, "Convert", includeReducedExtensionMethods: true);
        var names = new SortedSet<string>(StringComparer.Ordinal);
        var open = new SortedDictionary<string, MappingExtensionReceiver>(StringComparer.Ordinal);
        foreach (var method in visible.OfType<IMethodSymbol>())
        {
            var definition = (method.ReducedFrom ?? method).OriginalDefinition;
            if (!index.TryGetValue(definition, out var pattern)) continue;
            var key = pattern.Key;
            if (key == pairKey)
                names.Add(definition.ContainingAssembly.Identity.Name);
            else if (definition.IsGenericMethod)
                open[key] = pattern;
        }

        return new VisibleFriendExtensions(names.Count == 0 ? null : string.Join(", ", names),
            open.Values.ToImmutableArray());
    }

    private static ImmutableDictionary<IMethodSymbol, MappingExtensionReceiver> BuildIndex(Compilation compilation)
    {
        var methods = ImmutableDictionary.CreateBuilder<IMethodSymbol, MappingExtensionReceiver>(SymbolEqualityComparer.Default);
        var builder = compilation.GetTypeByMetadataName(MetadataNames.PairMapperBuilder);
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (!assembly.GivesAccessTo(compilation.Assembly))
            {
                continue;
            }

            var container = assembly.GetTypeByMetadataName(
                "Morphant." + GeneratedMappingExtensionNaming.CommonContainerTypeName);
            if (container is null || !compilation.IsSymbolAccessibleWithin(container, compilation.Assembly))
                continue;

            // Every generated pair provides Convert, including opaque roots.
            foreach (var method in container.GetMembers("Convert").OfType<IMethodSymbol>())
            {
                if (!GeneratedMappingExtensionNaming.HasGeneratedSignature(method) ||
                    !compilation.IsSymbolAccessibleWithin(method, compilation.Assembly) ||
                    method.ReturnType is not INamedTypeSymbol receiver ||
                    !SymbolEqualityComparer.Default.Equals(receiver.OriginalDefinition, builder))
                    continue;

                methods[method.OriginalDefinition] = new MappingExtensionReceiver(receiver.TypeArguments[0], receiver.TypeArguments[1]);
            }
        }

        return methods.ToImmutable();
    }
}

internal readonly record struct VisibleFriendExtensions(string? ExactAssemblies, ImmutableArray<MappingExtensionReceiver> OpenReceivers);

internal readonly record struct MappingExtensionReceiver(ITypeSymbol Source, ITypeSymbol Destination)
{
    public string Key => MappingTypeIdentityPolicy.CreateAlphaEquivalentPairKey(Source, Destination);
}
