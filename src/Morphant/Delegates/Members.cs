namespace Morphant.Delegates;

/// <summary>
/// Describes destination member mappings from a non-null source.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TMembers">The destination-member rules type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <returns>The destination-member rules.</returns>
public delegate TMembers Members<in TSource, out TMembers>(TSource source);

/// <summary>
/// Describes destination member mappings from a non-null source and an
/// optional existing destination.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TMembers">The destination-member rules type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The optional existing destination.</param>
/// <returns>The destination-member rules.</returns>
public delegate TMembers Members<in TSource, TPrevious, out TMembers>(
    TSource source,
    Option<TPrevious> previous);

/// <summary>
/// Describes destination member mappings with access to the selected mapping
/// result.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TResult">The selected mapping result type.</typeparam>
/// <typeparam name="TMembers">The destination-member rules type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The optional existing destination.</param>
/// <param name="result">The non-null selected mapping result.</param>
/// <returns>The destination-member rules.</returns>
public delegate TMembers Members<
    in TSource,
    TPrevious,
    in TResult,
    out TMembers>(
    TSource source,
    Option<TPrevious> previous,
    TResult result);

/// <summary>
/// Describes destination member mappings with access to the selected result
/// and current mapping context.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TResult">The selected mapping result type.</typeparam>
/// <typeparam name="TContext">The mapping context type.</typeparam>
/// <typeparam name="TMembers">The destination-member rules type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The optional existing destination.</param>
/// <param name="result">The non-null selected mapping result.</param>
/// <param name="context">The current mapping context.</param>
/// <returns>The destination-member rules.</returns>
public delegate TMembers Members<
    in TSource,
    TPrevious,
    in TResult,
    in TContext,
    out TMembers>(
    TSource source,
    Option<TPrevious> previous,
    TResult result,
    TContext context);
