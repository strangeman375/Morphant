namespace Morphant.Context;

internal sealed class MappingScope
{
    private readonly IServiceProvider serviceProvider;
    private bool isCompleted;

    public MappingScope(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        Mapper = new ScopedMapper(this);
    }

    public IMapper Mapper { get; }

    public TDestination Map<TSource, TDestination>(TSource? source)
    {
        ThrowIfCompleted();

        return Resolve<TSource, TDestination>().Map(
            source,
            new MappingContext(MappingOperation.Create, Mapper));
    }

    public TDestination Map<TSource, TDestination>(
        TSource? source,
        TDestination? destination)
    {
        ThrowIfCompleted();

        return Resolve<TSource, TDestination>().Map(
            source,
            destination,
            new MappingContext(MappingOperation.Update, Mapper));
    }

    public void Complete() => isCompleted = true;

    private ITypeMapper<TSource, TDestination>
        Resolve<TSource, TDestination>()
    {
        var serviceType =
            typeof(IEnumerable<ITypeMapper<TSource, TDestination>>);
        var service = serviceProvider.GetService(serviceType);

        if (service is not
            IEnumerable<ITypeMapper<TSource, TDestination>> candidates)
        {
            throw new InvalidOperationException(
                "No mapping is registered for the requested type pair.");
        }

        using var enumerator = candidates.GetEnumerator();

        if (!enumerator.MoveNext())
        {
            throw new InvalidOperationException(
                "No mapping is registered for the requested type pair.");
        }

        var candidate = enumerator.Current;

        if (candidate is null)
        {
            throw new InvalidOperationException(
                "The registered mapping candidate is null.");
        }

        if (enumerator.MoveNext())
        {
            throw new InvalidOperationException(
                "Multiple mappings are registered for the requested type " +
                "pair.");
        }

        return candidate;
    }

    private void ThrowIfCompleted()
    {
        if (isCompleted)
        {
            throw new InvalidOperationException(
                "The mapping scope has already completed.");
        }
    }

    private sealed class ScopedMapper : IMapper
    {
        private readonly MappingScope scope;

        public ScopedMapper(MappingScope scope)
        {
            this.scope = scope;
        }

        public TDestination Map<TSource, TDestination>(TSource? source) =>
            scope.Map<TSource, TDestination>(source);

        public TDestination Map<TSource, TDestination>(
            TSource? source,
            TDestination? destination) =>
            scope.Map(source, destination);
    }
}
