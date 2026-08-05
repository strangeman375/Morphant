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
/// Describes destination construction from a non-null source and an optional
/// existing destination.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TResult">The construction result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The optional existing destination.</param>
/// <returns>The construction result.</returns>
public delegate TResult Construct<in TSource, TPrevious, out TResult>(
    TSource source,
    Option<TPrevious> previous);
