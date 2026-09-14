using System.ComponentModel;
using Morphant.Context;
using Morphant.Exceptions;

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

    /// <summary>
    /// Skips null; otherwise evaluates the source, checks the destination type and updates it.
    /// </summary>
    public static void UpdateInPlace<TState, TSource, TDestination>(
        object? destination,
        TState state,
        Func<TState, TSource?> sourceSelector,
        MappingContext context)
    {
        if (destination is null)
        {
            return;
        }

        UpdateChecked<TSource, TDestination>(destination, sourceSelector(state), context);
    }

    /// <summary>
    /// Skips null; otherwise evaluates captured source state, checks the destination type and updates it.
    /// </summary>
    public static void UpdateInPlace<TSource, TDestination>(
        object? destination,
        Func<TSource?> sourceSelector,
        MappingContext context)
    {
        if (destination is null)
        {
            return;
        }

        UpdateChecked<TSource, TDestination>(destination, sourceSelector(), context);
    }

    /// <summary>
    /// Checks the destination for a selected derived pair and returns that pair's Update result.
    /// </summary>
    public static TBranchDestination UpdateDerived<TSource, TDestination, TBranchSource, TBranchDestination>(
        TBranchSource source,
        object? destination,
        MappingContext context)
    {
        TBranchDestination? branchDestination = destination switch
        {
            TBranchDestination compatible => compatible,
            null when default(TBranchDestination) is null => default,
            _ => throw PolymorphicDestinationTypeMismatchException
                .CreateForUpdate<TSource, TDestination, TBranchSource, TBranchDestination>(source, destination)
        };

        return context.Mapper.Map<TBranchSource, TBranchDestination>(source, branchDestination);
    }

    private static void UpdateChecked<TSource, TDestination>(
        object destination,
        TSource? source,
        MappingContext context)
    {
        var compatible = destination is TDestination value
            ? value
            : throw NestedDestinationTypeMismatchException.Create<TSource, TDestination>(
                MappingOperation.Update, destination);

        _ = context.Mapper.Map<TSource, TDestination>(source, compatible);
    }
}
