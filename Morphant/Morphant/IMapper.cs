#nullable disable annotations

namespace Morphant;

public interface IMapper
{
    TDestination Map<TSource, TDestination>(TSource source);

    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
}

public interface IMapper<in TSource, TDestination>
{
    TDestination Map(TSource source);

    TDestination Map(TSource source, TDestination destination);
}
