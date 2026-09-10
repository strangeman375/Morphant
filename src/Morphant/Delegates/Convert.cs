namespace Morphant.Delegates;

/// <summary>
/// Maps with ordinary C#, bypassing null policies and member rules.
/// </summary>
/// <typeparam name="TSource">The original source type.</typeparam>
/// <typeparam name="TResult">The mapping result type.</typeparam>
/// <param name="source">The original source, including null.</param>
/// <returns>The mapping result.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
public delegate TResult Convert<in TSource, out TResult>(TSource source);

/// <summary>
/// Maps with ordinary C#, bypassing null policies and member rules.
/// </summary>
/// <typeparam name="TSource">The original source type.</typeparam>
/// <typeparam name="TPrevious">The non-null destination type.</typeparam>
/// <typeparam name="TResult">The mapping result type.</typeparam>
/// <param name="source">The original source, including null.</param>
/// <param name="previous">The supplied destination; None for Create or a null destination.</param>
/// <returns>The mapping result.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
public delegate TResult Convert<in TSource, TPrevious, out TResult>(
    TSource source,
    Option<TPrevious> previous);

/// <summary>
/// Maps with ordinary C#, bypassing null policies and member rules.
/// </summary>
/// <typeparam name="TSource">The original source type.</typeparam>
/// <typeparam name="TPrevious">The non-null destination type.</typeparam>
/// <typeparam name="TContext">The mapping context type.</typeparam>
/// <typeparam name="TResult">The mapping result type.</typeparam>
/// <param name="source">The original source, including null.</param>
/// <param name="previous">The supplied destination; None for Create or a null destination.</param>
/// <param name="context">The current mapping context.</param>
/// <returns>The mapping result.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
public delegate TResult Convert<
    in TSource,
    TPrevious,
    in TContext,
    out TResult>(
    TSource source,
    Option<TPrevious> previous,
    TContext context);
