namespace Morphant.Delegates;

/// <summary>
/// Creates a destination through ordinary C# when none exists.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TResult">The destination result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <returns>The destination; null skips Members and further null handling.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
public delegate TResult ConstructUsing<in TSource, out TResult>(
    TSource source);

/// <summary>
/// Creates a destination through ordinary C# when none exists.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TContext">The mapping context type.</typeparam>
/// <typeparam name="TResult">The destination result type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="context">The current mapping context.</param>
/// <returns>The destination; null skips Members and further null handling.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
public delegate TResult ConstructUsing<
    in TSource,
    in TContext,
    out TResult>(
    TSource source,
    TContext context);
