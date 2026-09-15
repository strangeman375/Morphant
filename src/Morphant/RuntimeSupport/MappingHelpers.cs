using System.ComponentModel;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.RuntimeSupport;

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
    public static void UpdateInPlace<TState, TSource, TDestination, TCurrentDestination>(
        TCurrentDestination? destination,
        TState state,
        Func<TState, TSource?> sourceSelector,
        MappingContext context)
        where TCurrentDestination : class
    {
        if (destination is null)
        {
            return;
        }

        var source = sourceSelector(state);
        UpdateChecked<TSource, TDestination, TCurrentDestination>(destination, source, context);
    }

    /// <summary>
    /// Skips null; otherwise evaluates captured source state, checks the destination type and updates it.
    /// </summary>
    public static void UpdateInPlace<TSource, TDestination, TCurrentDestination>(
        TCurrentDestination? destination,
        Func<TSource?> sourceSelector,
        MappingContext context)
        where TCurrentDestination : class
    {
        if (destination is null)
        {
            return;
        }

        var source = sourceSelector();
        UpdateChecked<TSource, TDestination, TCurrentDestination>(destination, source, context);
    }

    /// <summary>
    /// Checks the destination for a selected derived pair and returns that pair's Update result.
    /// </summary>
    public static TBranchDestination UpdateDerived<TSource, TDestination, TBranchSource, TBranchDestination>(
        TBranchSource source,
        TDestination? destination,
        MappingContext context)
        where TDestination : class
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

    private static void UpdateChecked<TSource, TDestination, TCurrentDestination>(
        TCurrentDestination destination,
        TSource? source,
        MappingContext context)
        where TCurrentDestination : class
    {
        var compatible = destination is TDestination value
            ? value
            : throw NestedDestinationTypeMismatchException.Create<TSource, TDestination>(
                MappingOperation.Update, destination);

        _ = context.Mapper.Map<TSource, TDestination>(source, compatible);
    }
}
