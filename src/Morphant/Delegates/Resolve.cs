namespace Morphant.Delegates;

/// <summary>
/// Describes destination resolution from a non-null source and an optional
/// existing destination.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TResult">The resolution result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The optional existing destination.</param>
/// <returns>The resolution result.</returns>
public delegate TResult Resolve<in TSource, TPrevious, out TResult>(
    TSource source,
    Option<TPrevious> previous);

/// <summary>
/// Describes destination resolution with access to the current mapping
/// operation.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TContext">The operation marker type.</typeparam>
/// <typeparam name="TResult">The resolution result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The optional existing destination.</param>
/// <param name="context">The current mapping operation marker.</param>
/// <returns>The resolution result.</returns>
public delegate TResult Resolve<
    in TSource,
    TPrevious,
    in TContext,
    out TResult>(
    TSource source,
    Option<TPrevious> previous,
    TContext context);
