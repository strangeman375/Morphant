namespace Morphant.Delegates;

/// <summary>
/// Describes a fully manual mapping algorithm from the original source.
/// </summary>
/// <typeparam name="TSource">The original source type.</typeparam>
/// <typeparam name="TResult">The mapping result type.</typeparam>
/// <param name="source">The original source.</param>
/// <returns>The mapping result.</returns>
/// <remarks>
/// The callback receives the original source. Morphant does not apply null
/// handling, constructor selection, member conventions, or <c>Members</c>
/// around it.
/// </remarks>
public delegate TResult Convert<in TSource, out TResult>(TSource source);

/// <summary>
/// Describes a fully manual mapping algorithm with access to an optional
/// existing destination.
/// </summary>
/// <typeparam name="TSource">The original source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TResult">The mapping result type.</typeparam>
/// <param name="source">The original source.</param>
/// <param name="previous">The optional existing destination.</param>
/// <returns>The mapping result.</returns>
/// <remarks>
/// The callback receives the original source. An absent destination is
/// <see cref="Option{TPrevious}.None"/>. Morphant does not apply null handling,
/// constructor selection, member conventions, or <c>Members</c> around it.
/// </remarks>
public delegate TResult Convert<in TSource, TPrevious, out TResult>(
    TSource source,
    Option<TPrevious> previous);

/// <summary>
/// Describes a fully manual mapping algorithm with access to an optional
/// existing destination and the current mapping context.
/// </summary>
/// <typeparam name="TSource">The original source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TContext">The mapping context type.</typeparam>
/// <typeparam name="TResult">The mapping result type.</typeparam>
/// <param name="source">The original source.</param>
/// <param name="previous">The optional existing destination.</param>
/// <param name="context">The current mapping context.</param>
/// <returns>The mapping result.</returns>
/// <remarks>
/// The callback receives the original source. An absent destination is
/// <see cref="Option{TPrevious}.None"/>. Morphant does not apply null handling,
/// constructor selection, member conventions, or <c>Members</c> around it.
/// </remarks>
public delegate TResult Convert<
    in TSource,
    TPrevious,
    in TContext,
    out TResult>(
    TSource source,
    Option<TPrevious> previous,
    TContext context);
