namespace Morphant.Delegates;

/// <summary>
/// Describes destination member mappings from a non-null source and an
/// optional existing destination.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TMembers">The member plan type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The optional existing destination.</param>
/// <returns>The destination member plan.</returns>
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
/// <typeparam name="TMembers">The member plan type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The optional existing destination.</param>
/// <param name="result">The non-null selected mapping result.</param>
/// <returns>The destination member plan.</returns>
public delegate TMembers Members<
    in TSource,
    TPrevious,
    in TResult,
    out TMembers>(
    TSource source,
    Option<TPrevious> previous,
    TResult result);
