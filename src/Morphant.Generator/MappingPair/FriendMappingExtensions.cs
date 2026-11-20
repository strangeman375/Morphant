using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class FriendMappingExtensions
{
    private sealed record Owner(INamedTypeSymbol Container, bool Global);

    private static readonly ConditionalWeakTable<Compilation, ImmutableDictionary<string, ImmutableArray<Owner>>> Indexes = new();

    public static string? Find(Compilation compilation, ITypeSymbol source, ITypeSymbol destination,
        SyntaxNode registration, CancellationToken cancellationToken)
    {
        var index = Indexes.GetValue(compilation, BuildIndex);
        if (index.IsEmpty || !index.TryGetValue(MappingTypeIdentityPolicy.CreateAlphaEquivalentPairKey(source, destination), out var owners))
            return null;

        var names = new SortedSet<string>(StringComparer.Ordinal);
        ImmutableArray<ISymbol> visible = default;
        foreach (var owner in owners)
        {
            if (!owner.Global)
            {
                if (visible.IsDefault)
                {
                    var receiver = compilation.GetTypeByMetadataName(MetadataNames.PairMapperBuilder)!.Construct(source, destination);
                    cancellationToken.ThrowIfCancellationRequested();
                    visible = compilation.GetSemanticModel(registration.SyntaxTree).LookupSymbols(
                        registration.SpanStart, receiver, "Convert", includeReducedExtensionMethods: true);
                }

                if (!visible.Any(method => SymbolEqualityComparer.Default.Equals(method.ContainingType, owner.Container)))
                    continue;
            }

            names.Add(owner.Container.ContainingAssembly.Identity.Name);
        }

        return names.Count == 0 ? null : string.Join(", ", names);
    }

    private static ImmutableDictionary<string, ImmutableArray<Owner>> BuildIndex(Compilation compilation)
    {
        var pairs = new Dictionary<string, List<Owner>>(StringComparer.Ordinal);
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

            var owner = new Owner(container, MappingTypeEligibilityPolicy.IsAvailableThroughGlobalAlias(assembly, compilation));
            // Every generated pair provides Convert, including opaque roots.
            foreach (var method in container.GetMembers("Convert").OfType<IMethodSymbol>())
            {
                if (!GeneratedMappingExtensionNaming.HasGeneratedSignature(method) ||
                    !compilation.IsSymbolAccessibleWithin(method, compilation.Assembly) ||
                    method.ReturnType is not INamedTypeSymbol receiver ||
                    !SymbolEqualityComparer.Default.Equals(receiver.OriginalDefinition, builder))
                    continue;

                var key = MappingTypeIdentityPolicy.CreateAlphaEquivalentPairKey(
                    receiver.TypeArguments[0], receiver.TypeArguments[1]);
                if (!pairs.TryGetValue(key, out var owners))
                    pairs.Add(key, owners = []);
                if (!owners.Contains(owner))
                    owners.Add(owner);
            }
        }

        return pairs.ToImmutableDictionary(
            entry => entry.Key, entry => entry.Value.ToImmutableArray(), StringComparer.Ordinal);
    }
}
