namespace Morphant.Delegates;

/// <summary>
/// Chooses the destination through ordinary C# on Create and Update.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TResult">The destination result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The supplied destination; None on Create or null Update.</param>
/// <returns>The destination; null skips Members and further null handling.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
public delegate TResult ResolveUsing<in TSource, TPrevious, out TResult>(
    TSource source,
    Option<TPrevious> previous);

/// <summary>
/// Chooses the destination through ordinary C# on Create and Update.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TContext">The mapping context type.</typeparam>
/// <typeparam name="TResult">The destination result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The supplied destination; None on Create or null Update.</param>
/// <param name="context">The current mapping context.</param>
/// <returns>The destination; null skips Members and further null handling.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
public delegate TResult ResolveUsing<
    in TSource,
    TPrevious,
    in TContext,
    out TResult>(
    TSource source,
    Option<TPrevious> previous,
    TContext context);
