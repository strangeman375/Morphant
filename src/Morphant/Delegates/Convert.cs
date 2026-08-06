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
/// <remarks>
/// A manual mapping receives the original source before null handling. An
/// absent destination is represented by
/// <see cref="Option{TPrevious}.None"/>, while
/// <paramref name="context"/> distinguishes Create from Update. The returned
/// value is authoritative and is not processed by the declarative pipeline.
/// </remarks>
public delegate TResult Convert<in TSource, TPrevious, out TResult>(
    TSource source,
    Option<TPrevious> previous,
    MappingContext context);
