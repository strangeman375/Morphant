using System.Text;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal static class SymbolNameHelper
{
    internal static string GetFullMetadataName(INamedTypeSymbol type)
    {
        var namespaces = new Stack<INamespaceSymbol>();

        for (var current = type.ContainingNamespace;
             !current.IsGlobalNamespace;
             current = current.ContainingNamespace)
        {
            namespaces.Push(current);
        }

        var containingTypes = new Stack<INamedTypeSymbol>();

        for (var current = type;
             current is not null;
             current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        var builder = new StringBuilder();

        while (namespaces.Count > 0)
        {
            if (builder.Length > 0)
            {
                builder.Append('.');
            }

            builder.Append(namespaces.Pop().MetadataName);
        }

        if (builder.Length > 0)
        {
            builder.Append('.');
        }

        var firstType = true;

        while (containingTypes.Count > 0)
        {
            if (!firstType)
            {
                builder.Append('+');
            }

            builder.Append(containingTypes.Pop().MetadataName);
            firstType = false;
        }

        return builder.ToString();
    }
}
