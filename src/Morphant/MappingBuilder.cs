using System.Diagnostics.CodeAnalysis;
using Morphant.Exceptions;

namespace Morphant;

/// <summary>
/// Configures mapping from <typeparamref name="TSource"/> to
/// <typeparamref name="TDestination"/>.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
[ExcludeFromCodeCoverage]
public sealed class MappingBuilder<TSource, TDestination> :
    MapperBuilderBase<MappingBuilder<TSource, TDestination>>
{
    private MappingBuilder()
    {
    }

    /// <summary>
    /// Reuses the nearest available base mapping; local rules take priority.
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
    /// Only the same pair also reuses destination selection or Convert.
    /// ForDerived links are not inherited. Connect base mapper configuration
    /// with <c>base.Configure(builder)</c>.
    /// </remarks>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/include-base.md"/>
    public MappingBuilder<TSource, TDestination>
        IncludeBase<TBaseSource, TBaseDestination>() =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Routes a derived runtime source to a separately registered mapping pair.
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
    /// Register the derived pair with <c>Map</c>. To reuse base mapping rules,
    /// also configure <see cref="IncludeBase"/> on that pair.
    /// </remarks>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/for-derived.md"/>
    public MappingBuilder<TSource, TDestination>
        ForDerived<TDerivedSource, TDerivedDestination>()
        where TDerivedSource : TSource
        where TDerivedDestination : TDestination =>
        throw new RuntimeInvocationNotSupportedException();
}
