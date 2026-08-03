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
    public TDestination Map<TSource, TDestination>(TSource? source)
    {
        // todo: get type mapper from DI and map
        throw new NotImplementedException();
    }

    public TDestination Map<TSource, TDestination>(
        TSource? source,
        TDestination? destination)
    {
        // todo: get type mapper from DI and map
        throw new NotImplementedException();
    }
}
