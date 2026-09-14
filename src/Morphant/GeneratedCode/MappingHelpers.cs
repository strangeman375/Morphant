using System.ComponentModel;
using Morphant.Context;

namespace Morphant.GeneratedCode;

/// <summary>
/// Shared operations for generated mapping code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class MappingHelpers
{
    /// <summary>
    /// Updates a non-null destination; otherwise skips source evaluation.
    /// </summary>
    public static void UpdateInPlace<TState, TSource, TDestination>(
        TDestination? destination,
        TState state,
        Func<TState, TSource?> sourceSelector,
        MappingContext context)
        where TDestination : class
    {
        if (destination is null)
        {
            return;
        }

        var source = sourceSelector(state);
        _ = context.Mapper.Map<TSource, TDestination>(source, destination);
    }

    /// <summary>
    /// Updates a non-null destination using captured state; otherwise skips source evaluation.
    /// </summary>
    public static void UpdateInPlace<TSource, TDestination>(
        TDestination? destination,
        Func<TSource?> sourceSelector,
        MappingContext context)
        where TDestination : class
    {
        if (destination is null)
        {
            return;
        }

        var source = sourceSelector();
        _ = context.Mapper.Map<TSource, TDestination>(source, destination);
    }
}
