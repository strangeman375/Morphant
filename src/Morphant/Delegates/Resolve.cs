namespace Morphant.Delegates;

/// <summary>
/// Declares reuse or construction on Create and Update.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The non-null destination type.</typeparam>
/// <typeparam name="TResult">The resolution result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The supplied destination; None for Create or a null destination.</param>
/// <returns>The resolution result.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve.md"/>
public delegate TResult Resolve<in TSource, TPrevious, out TResult>(
    TSource source,
    Option<TPrevious> previous);

/// <summary>
/// Declares reuse or construction on Create and Update.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The non-null destination type.</typeparam>
/// <typeparam name="TContext">The mapping context type.</typeparam>
/// <typeparam name="TResult">The resolution result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The supplied destination; None for Create or a null destination.</param>
/// <param name="context">The current mapping context.</param>
/// <returns>The resolution result.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve.md"/>
public delegate TResult Resolve<
    in TSource,
    TPrevious,
    in TContext,
    out TResult>(
    TSource source,
    Option<TPrevious> previous,
    TContext context);
