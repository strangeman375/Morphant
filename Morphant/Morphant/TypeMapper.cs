using Morphant.Exceptions;
using Morphant.Markers;

namespace Morphant;

public interface ITypeMapper<in TSource, TDestination>
{
    TDestination Map(TSource source, MappingContext context);

    TDestination Map(TSource source, TDestination destination, MappingContext context);

    IQueryable<TDestination> Project(IQueryable<TSource> queryable);
}

public abstract class TypeMapper
{
    protected abstract void Configure(MapperBuilder builder);

    protected static ByConventionMarker ByConvention() =>
        throw new RuntimeInvocationNotSupportedException();

    protected static ByFactoryMarker<TDestination> ByFactory<TDestination>(Func<TDestination> factory) =>
        throw new RuntimeInvocationNotSupportedException();

    protected static AutoMarker Auto() =>
        throw new RuntimeInvocationNotSupportedException();

    protected static AutoMarker<T> Auto<T>() =>
        throw new RuntimeInvocationNotSupportedException();

    protected static IgnoreMarker Ignore() =>
        throw new RuntimeInvocationNotSupportedException();

    protected static IgnoreMarker<T> Ignore<T>() =>
        throw new RuntimeInvocationNotSupportedException();

    protected static MapMarker Map(object? source) =>
        throw new RuntimeInvocationNotSupportedException();

    protected static MapMarker Map(object? source, object? destination) =>
        throw new RuntimeInvocationNotSupportedException();

    protected static MapMarker<T> Map<T>(object? source) =>
        throw new RuntimeInvocationNotSupportedException();

    protected static MapMarker<T> Map<T>(object? source, T? destination) =>
        throw new RuntimeInvocationNotSupportedException();
}
