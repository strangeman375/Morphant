using Morphant.Context;

namespace Morphant.Delegates;

/// <summary>
/// Describes a fully manual mapping algorithm.
/// </summary>
/// <typeparam name="TSource">The original source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TResult">The mapping result type.</typeparam>
/// <param name="source">The original source.</param>
/// <param name="previous">The optional existing destination.</param>
/// <param name="context">The current mapping context.</param>
/// <returns>The mapping result.</returns>
public delegate TResult Convert<in TSource, TPrevious, out TResult>(
    TSource source,
    Option<TPrevious> previous,
    MappingContext context);
