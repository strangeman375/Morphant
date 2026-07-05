using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal static class Extensions
{
    public static IncrementalValuesProvider<T> WhereHasValue<T>(
        this IncrementalValuesProvider<T?> provider)
        where T : struct
    {
        return provider
            .Where(static x => x.HasValue)
            .Select(static (x, _) => x!.Value);
    }
}
