namespace Morphant;

public interface IContextualMapper
{
    TDestination Map<TSource, TDestination>(TSource source, MappingContext context);

    TDestination Map<TSource, TDestination>(TSource source, TDestination destination, MappingContext context);
}
