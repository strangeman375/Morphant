#nullable disable annotations

namespace Morphant;

public interface IMapper
{
    TDestination Map<TSource, TDestination>(TSource source);

    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
}

public sealed class Mapper : IMapper
{
    public TDestination Map<TSource, TDestination>(TSource source) =>
        Map<TSource, TDestination>(source, default);

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        // todo: get type mapper from DI and map
        throw new NotImplementedException();
    }
}
