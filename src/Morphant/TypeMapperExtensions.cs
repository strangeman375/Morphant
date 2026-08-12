using Morphant.Context;

namespace Morphant;

/// <summary>
/// Invokes an exact type mapper without an application-wide
/// <see cref="IMapper"/>.
/// </summary>
/// <remarks>
/// Nested mappings can use other pairs declared by the same generated mapper.
/// </remarks>
public static class TypeMapperExtensions
{
    /// <summary>
    /// Maps the specified source without a supplied destination.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="mapper">The mapper to invoke.</param>
    /// <param name="source">The source to map.</param>
    /// <returns>The mapped destination.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mapper"/> is
    /// <see langword="null"/>.</exception>
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
    /// <param name="mapper">The mapper to invoke.</param>
    /// <param name="source">The source to map.</param>
    /// <param name="destination">The supplied destination.</param>
    /// <returns>
    /// The mapped destination, which may replace
    /// <paramref name="destination"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="mapper"/> is
    /// <see langword="null"/>.</exception>
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
