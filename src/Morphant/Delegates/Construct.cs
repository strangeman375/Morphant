namespace Morphant.Delegates;

/// <summary>
/// Declares construction when no destination exists.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TResult">The construction result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <returns>The construction result.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct.md"/>
public delegate TResult Construct<in TSource, out TResult>(TSource source);

/// <summary>
/// Declares construction when no destination exists.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TContext">The mapping context type.</typeparam>
/// <typeparam name="TResult">The construction result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="context">The current mapping context.</param>
/// <returns>The construction result.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct.md"/>
public delegate TResult Construct<in TSource, in TContext, out TResult>(
    TSource source,
    TContext context);
