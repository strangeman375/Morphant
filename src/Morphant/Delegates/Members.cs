namespace Morphant.Delegates;

/// <summary>
/// Configures destination members and matching constructor arguments.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TMembers">The destination-member rules type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <returns>The destination-member rules.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/members.md"/>
public delegate TMembers Members<in TSource, out TMembers>(TSource source);

/// <summary>
/// Configures destination members and matching constructor arguments.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TMembers">The destination-member rules type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The supplied destination; None on Create or null Update.</param>
/// <returns>The destination-member rules.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/members.md"/>
public delegate TMembers Members<in TSource, TPrevious, out TMembers>(
    TSource source,
    Option<TPrevious> previous);

/// <summary>
/// Configures destination members and matching constructor arguments.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TResult">The selected mapping result type.</typeparam>
/// <typeparam name="TMembers">The destination-member rules type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The supplied destination; None on Create or null Update.</param>
/// <param name="result">The non-null selected result; unavailable before construction.</param>
/// <returns>The destination-member rules.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/members.md"/>
public delegate TMembers Members<
    in TSource,
    TPrevious,
    in TResult,
    out TMembers>(
    TSource source,
    Option<TPrevious> previous,
    TResult result);

/// <summary>
/// Configures destination members and matching constructor arguments.
/// </summary>
/// <typeparam name="TSource">The non-null source type.</typeparam>
/// <typeparam name="TPrevious">The existing destination value type.</typeparam>
/// <typeparam name="TResult">The selected mapping result type.</typeparam>
/// <typeparam name="TContext">The mapping context type.</typeparam>
/// <typeparam name="TMembers">The destination-member rules type.</typeparam>
/// <param name="source">The non-null source.</param>
/// <param name="previous">The supplied destination; None on Create or null Update.</param>
/// <param name="result">The non-null selected result; unavailable before construction.</param>
/// <param name="context">The current mapping context.</param>
/// <returns>The destination-member rules.</returns>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/members.md"/>
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
