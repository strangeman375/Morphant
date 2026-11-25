using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal readonly record struct InterfaceProjection(INamedTypeSymbol Source, INamedTypeSymbol Target)
{
    // A recursive interface can grow its own arguments indefinitely. Revisit
    // the same definitions only while consuming a nested argument on at least
    // one side. Contravariance can put that argument on the source side.
    public bool CanFollow(IEnumerable<InterfaceProjection> ancestors)
    {
        foreach (var previous in ancestors)
        {
            if (SymbolEqualityComparer.Default.Equals(previous.Source.OriginalDefinition, Source.OriginalDefinition) &&
                SymbolEqualityComparer.Default.Equals(previous.Target.OriginalDefinition, Target.OriginalDefinition) &&
                !ContainsArgument(previous.Target, Target) && !ContainsArgument(previous.Source, Source))
                return false;
        }
        return true;
    }

    private static bool ContainsArgument(ITypeSymbol type, INamedTypeSymbol candidate)
    {
        return type switch
        {
            IArrayTypeSymbol array => Contains(array.ElementType),
            INamedTypeSymbol named => named.TypeArguments.Any(Contains) ||
                named.ContainingType is { } enclosing && Contains(enclosing),
            _ => false
        };

        bool Contains(ITypeSymbol child) =>
            SymbolEqualityComparer.Default.Equals(child, candidate) || ContainsArgument(child, candidate);
    }
}
