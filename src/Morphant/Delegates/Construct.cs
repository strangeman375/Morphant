namespace Morphant.Delegates;

/// <summary>
/// Describes destination construction from a non-null source.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TResult">The construction result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <returns>The construction result.</returns>
public delegate TResult Construct<in TSource, out TResult>(TSource source);

/// <summary>
/// Describes destination construction from a non-null source with access to
/// the current mapping context.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TContext">The mapping context type.</typeparam>
/// <typeparam name="TResult">The construction result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="context">The current mapping context.</param>
/// <returns>The construction result.</returns>
public delegate TResult Construct<in TSource, in TContext, out TResult>(
    TSource source,
    TContext context);
