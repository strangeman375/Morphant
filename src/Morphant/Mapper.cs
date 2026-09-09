using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant;

/// <summary>
/// Maps exact source/destination pairs registered with dependency injection.
/// </summary>
/// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/create-and-update.md"/>
public interface IMapper
{
    /// <summary>
    /// Runs Create without a supplied destination.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">
    /// The destination type. Use a nullable reference type when the mapping
    /// can return null.
    /// </typeparam>
    /// <param name="source">The source to map.</param>
    /// <returns>
    /// The result; may be <see langword="default"/> if the mapping allows it.
    /// </returns>
    /// <exception cref="MappingException">
    /// Mapping lookup or execution fails.
    /// </exception>
    TDestination Map<TSource, TDestination>(TSource? source);

    /// <summary>
    /// Runs Update, including when the supplied destination is null.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">
    /// The destination type. Use a nullable reference type when the mapping
    /// can return null.
    /// </typeparam>
    /// <param name="source">The source to map.</param>
    /// <param name="destination">The supplied destination.</param>
    /// <returns>
    /// The result; may replace <paramref name="destination"/> or be default.
    /// </returns>
    /// <exception cref="MappingException">
    /// Mapping lookup or execution fails.
    /// </exception>
    TDestination Map<TSource, TDestination>(
        TSource? source,
        TDestination? destination);
}

/// <summary>
/// Maps objects using registrations from an <see cref="IServiceProvider"/>.
/// </summary>
public sealed class Mapper : IMapper
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a mapper backed by the specified service provider.
    /// </summary>
    /// <param name="serviceProvider">The provider used to resolve type
    /// mappers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/>
    /// is <see langword="null"/>.</exception>
    public Mapper(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ??
            throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc/>
    public TDestination Map<TSource, TDestination>(TSource? source)
    {
        var scope = new MappingScope(_serviceProvider);

        try
        {
            return scope.Mapper.Map<TSource, TDestination>(source);
        }
        finally
        {
            scope.Complete();
        }
    }

    /// <inheritdoc/>
    public TDestination Map<TSource, TDestination>(
        TSource? source,
        TDestination? destination)
    {
        var scope = new MappingScope(_serviceProvider);

        try
        {
            return scope.Mapper.Map(source, destination);
        }
        finally
        {
            scope.Complete();
        }
    }
}
