using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class FriendMappingExtensions
{
    private static readonly ConditionalWeakTable<Compilation, ImmutableDictionary<string, string>>
        Indexes = new();

    public static string? Find(Compilation compilation, ITypeSymbol source, ITypeSymbol destination)
    {
        var index = Indexes.GetValue(compilation, BuildIndex);
        return !index.IsEmpty && index.TryGetValue(
            MappingTypeIdentityPolicy.CreateAlphaEquivalentPairKey(source, destination), out var assemblies)
            ? assemblies
            : null;
    }

    private static ImmutableDictionary<string, string> BuildIndex(Compilation compilation)
    {
        var pairs = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var builder = compilation.GetTypeByMetadataName(MetadataNames.PairMapperBuilder);
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (!assembly.GivesAccessTo(compilation.Assembly) ||
                !MappingTypeEligibilityPolicy.IsAvailableThroughGlobalAlias(assembly, compilation))
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

                var key = MappingTypeIdentityPolicy.CreateAlphaEquivalentPairKey(
                    receiver.TypeArguments[0], receiver.TypeArguments[1]);
                if (!pairs.TryGetValue(key, out var names))
                    pairs.Add(key, names = new SortedSet<string>(StringComparer.Ordinal));
                names.Add(assembly.Identity.Name);
            }
        }

        return pairs.ToImmutableDictionary(
            entry => entry.Key, entry => string.Join(", ", entry.Value), StringComparer.Ordinal);
    }
}
