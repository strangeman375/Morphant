namespace Morphant.Delegates;

/// <summary>
/// Describes runtime destination resolution from a non-null source and an
/// optional existing destination.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TResult">The destination result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The optional existing destination.</param>
/// <returns>The destination result.</returns>
/// <remarks>
/// A <see langword="null"/> result is final: Morphant skips <c>Members</c>
/// and does not apply null handling again.
/// </remarks>
public delegate TResult ResolveUsing<in TSource, TPrevious, out TResult>(
    TSource source,
    Option<TPrevious> previous);

/// <summary>
/// Describes runtime destination resolution with access to mapping context.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TContext">The mapping context type.</typeparam>
/// <typeparam name="TResult">The destination result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The optional existing destination.</param>
/// <param name="context">The current mapping context.</param>
/// <returns>The destination result.</returns>
/// <remarks>
/// A <see langword="null"/> result is final: Morphant skips <c>Members</c>
/// and does not apply null handling again.
/// </remarks>
public delegate TResult ResolveUsing<
    in TSource,
    TPrevious,
    in TContext,
    out TResult>(
    TSource source,
    Option<TPrevious> previous,
    TContext context);
