#nullable disable annotations

using Morphant.Exceptions;

namespace Morphant;

public interface ITypeMapper<in TSource, TDestination>
{
    TDestination Map(TSource source, TDestination destination);
}

public abstract class TypeMapper
{
    protected abstract void Configure(MapperBuilder builder);

    protected static AutoMarker Auto() =>
        throw new RuntimeInvocationNotSupportedException();

    protected static AutoMarker<T> Auto<T>() =>
        throw new RuntimeInvocationNotSupportedException();

    protected static IgnoreMarker Ignore() =>
        throw new RuntimeInvocationNotSupportedException();

    protected static IgnoreMarker<T> Ignore<T>() =>
        throw new RuntimeInvocationNotSupportedException();
}
