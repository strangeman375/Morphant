using Morphant.Exceptions;

namespace Morphant;

public abstract class MapperBuilder
{
    internal MapperBuilder()
    {
    }

    public MapperBuilder<TSource, TDestination> Map<TSource, TDestination>() =>
        throw new RuntimeInvocationNotSupportedException();
}

public abstract class MapperBuilder<TSource, TDestination>
{
    internal MapperBuilder()
    {
    }

    public MapperBuilder<TSource, TDestination> Template(Func<TSource, TDestination> templateFunc) =>
        throw new RuntimeInvocationNotSupportedException();
}
