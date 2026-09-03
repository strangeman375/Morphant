using System.Diagnostics.CodeAnalysis;
using Morphant.Exceptions;

namespace Morphant;

/// <summary>
/// Configures mapping from <typeparamref name="TSource"/> to
/// <typeparamref name="TDestination"/> declared by
/// <typeparamref name="TMapper"/>.
/// </summary>
/// <typeparam name="TMapper">The mapper owning the configuration.</typeparam>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
[ExcludeFromCodeCoverage]
public sealed class MappingBuilder<TMapper, TSource, TDestination> :
    MapperBuilderBase<MappingBuilder<TMapper, TSource, TDestination>>
    where TMapper : TypeMapper<TMapper>
{
    private MappingBuilder()
    {
    }

    /// <summary>
    /// Adds the readable instance members of a nested source object to the
    /// convention lookup scope for this mapping.
    /// </summary>
    /// <param name="selector">
    /// One inline property or field path rooted in the mapping source, or an
    /// anonymous object containing several such paths.
    /// </param>
    /// <remarks>
    /// The root source keeps precedence. Included members participate in
    /// automatic destination-member and constructor-parameter mapping; they
    /// do not start a nested mapping.
    /// </remarks>
    /// <returns>This mapping builder.</returns>
    public MappingBuilder<TMapper, TSource, TDestination> IncludeMembers(
        Func<TSource, object?> selector) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Includes configuration from the nearest available mapping for the
    /// specified source and destination types.
    /// </summary>
    /// <typeparam name="TBaseSource">
    /// The base source type. <typeparamref name="TSource"/> must be assignable
    /// to this type.
    /// </typeparam>
    /// <typeparam name="TBaseDestination">
    /// The base destination type. <typeparamref name="TDestination"/> must be
    /// assignable to this type.
    /// </typeparam>
    /// <remarks>
    /// Local settings and rules take precedence. A mapping declared in a base
    /// mapper is available only through <c>base.Configure(builder)</c>. A
    /// different pair contributes settings, included source members and
    /// explicit member rules, but not its destination-selection or
    /// <c>Convert</c> behavior. The exact same pair contributes all of its
    /// configuration.
    /// </remarks>
    /// <returns>This mapping builder.</returns>
    public MappingBuilder<TMapper, TSource, TDestination>
        IncludeBase<TBaseSource, TBaseDestination>() =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Routes a matching non-exact runtime source to a separately registered
    /// mapping pair.
    /// </summary>
    /// <typeparam name="TDerivedSource">
    /// The runtime source branch. It must be assignable to
    /// <typeparamref name="TSource"/> and differ from it.
    /// </typeparam>
    /// <typeparam name="TDerivedDestination">
    /// The destination of the branch. It must be assignable to
    /// <typeparamref name="TDestination"/>.
    /// </typeparam>
    /// <remarks>
    /// This call adds only a dispatch link. Register
    /// <typeparamref name="TDerivedSource"/> to
    /// <typeparamref name="TDerivedDestination"/> separately with
    /// <c>Map&lt;TDerivedSource, TDerivedDestination&gt;()</c>. It does not
    /// inherit mapping rules; use <see cref="IncludeBase"/> for rule reuse
    /// when needed.
    /// </remarks>
    /// <returns>This mapping builder.</returns>
    public MappingBuilder<TMapper, TSource, TDestination>
        ForDerived<TDerivedSource, TDerivedDestination>()
        where TDerivedSource : TSource
        where TDerivedDestination : TDestination =>
        throw new RuntimeInvocationNotSupportedException();
}
