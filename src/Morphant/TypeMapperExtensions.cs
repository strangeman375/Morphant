using Morphant.Context;

namespace Morphant;

/// <summary>
/// Provides context-free entry points for invoking an exact type mapper
/// without an application-wide <see cref="IMapper"/>.
/// </summary>
public static class TypeMapperExtensions
{
    /// <summary>
    /// Maps the specified source without a supplied destination.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="mapper">
    /// The mapper instance. Nested mappings may use every exact mapping pair
    /// declared by this same generated <see cref="TypeMapper"/> instance. An
    /// implementation that does not derive from <see cref="TypeMapper"/>
    /// exposes its selected receiver pair.
    /// </param>
    /// <param name="source">The source to map.</param>
    /// <returns>The mapped destination.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="mapper"/> is <see langword="null"/>.
    /// </exception>
    public static TDestination Create<TSource, TDestination>(
        this ITypeMapper<TSource, TDestination> mapper,
        TSource? source)
    {
        if (mapper is null)
        {
            throw new ArgumentNullException(nameof(mapper));
        }

        var scope = MappingScope.CreateStandalone(mapper);

        try
        {
            return mapper.Create(
                source,
                new MappingContext(
                    MappingOperation.Create,
                    scope.Mapper));
        }
        finally
        {
            scope.Complete();
        }
    }

    /// <summary>
    /// Maps the specified source with a supplied destination.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="mapper">
    /// The mapper instance. Nested mappings may use every exact mapping pair
    /// declared by this same generated <see cref="TypeMapper"/> instance. An
    /// implementation that does not derive from <see cref="TypeMapper"/>
    /// exposes its selected receiver pair.
    /// </param>
    /// <param name="source">The source to map.</param>
    /// <param name="destination">The supplied destination.</param>
    /// <returns>
    /// The authoritative mapped destination, which may replace
    /// <paramref name="destination"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="mapper"/> is <see langword="null"/>.
    /// </exception>
    public static TDestination Update<TSource, TDestination>(
        this ITypeMapper<TSource, TDestination> mapper,
        TSource? source,
        TDestination? destination)
    {
        if (mapper is null)
        {
            throw new ArgumentNullException(nameof(mapper));
        }

        var scope = MappingScope.CreateStandalone(mapper);

        try
        {
            return mapper.Update(
                source,
                destination,
                new MappingContext(
                    MappingOperation.Update,
                    scope.Mapper));
        }
        finally
        {
            scope.Complete();
        }
    }
}
