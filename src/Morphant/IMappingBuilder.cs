namespace Morphant;

/// <summary>
/// Identifies the mapper and type pair for generated configuration methods.
/// </summary>
/// <typeparam name="TMapper">The mapper owning the configuration.</typeparam>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
public interface IMappingBuilder<out TMapper, TSource, TDestination>
{
}
