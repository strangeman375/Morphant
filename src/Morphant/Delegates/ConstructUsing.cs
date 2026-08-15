namespace Morphant.Delegates;

/// <summary>
/// Describes runtime destination construction from a non-null source.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TResult">The destination result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <returns>The destination result.</returns>
/// <remarks>
/// A <see langword="null"/> result is final: Morphant skips <c>Members</c>
/// and does not apply null handling again.
/// </remarks>
public delegate TResult ConstructUsing<in TSource, out TResult>(
    TSource source);

/// <summary>
/// Describes runtime destination construction with access to mapping context.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TContext">The mapping context type.</typeparam>
/// <typeparam name="TResult">The destination result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="context">The current mapping context.</param>
/// <returns>The destination result.</returns>
/// <remarks>
/// A <see langword="null"/> result is final: Morphant skips <c>Members</c>
/// and does not apply null handling again.
/// </remarks>
public delegate TResult ConstructUsing<
    in TSource,
    in TContext,
    out TResult>(
    TSource source,
    TContext context);
