using Morphant.Context;

namespace Morphant;

/// <summary>
/// Represents application-wide mapping operations.
/// </summary>
public interface IMapper
{
    /// <summary>
    /// Maps the specified source without a supplied destination.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The source to map.</param>
    /// <returns>The mapped destination.</returns>
    TDestination Map<TSource, TDestination>(TSource? source);

    /// <summary>
    /// Maps the specified source with a supplied destination.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The source to map.</param>
    /// <param name="destination">The supplied destination.</param>
    /// <returns>The mapped destination.</returns>
    TDestination Map<TSource, TDestination>(
        TSource? source,
        TDestination? destination);
}

public sealed class Mapper : IMapper
{
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a root mapper that resolves manually registered
    /// <see cref="ITypeMapper{TSource, TDestination}"/> implementations from
    /// the specified service provider.
    /// </summary>
    /// <param name="serviceProvider">
    /// The service provider for the current application scope. For every
    /// mapping pair it must expose the corresponding
    /// <see cref="IEnumerable{T}"/> of
    /// <see cref="ITypeMapper{TSource, TDestination}"/> implementations.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serviceProvider"/> is <see langword="null"/>.
    /// </exception>
    public Mapper(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider ??
            throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc/>
    public TDestination Map<TSource, TDestination>(TSource? source)
    {
        var scope = new MappingScope(serviceProvider);

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
        var scope = new MappingScope(serviceProvider);

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
