using System.Text;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal static class SymbolNameHelper
{
    internal static string GetFullMetadataName(INamedTypeSymbol type)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();

        for (var current = type; current is not null; current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        var builder = new StringBuilder();

        if (!type.ContainingNamespace.IsGlobalNamespace)
        {
            builder.Append(type.ContainingNamespace.ToDisplayString());
            builder.Append('.');
        }

        var first = true;

        while (containingTypes.Count > 0)
        {
            if (!first)
            {
                builder.Append('+');
            }

            builder.Append(containingTypes.Pop().MetadataName);
            first = false;
        }

        return builder.ToString();
    }
}
