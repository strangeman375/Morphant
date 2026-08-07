using Morphant.Exceptions;

namespace Morphant.Context;

internal sealed class MappingScope
{
    private readonly IServiceProvider _serviceProvider;
    private bool _isCompleted;

    public MappingScope(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Mapper = new ScopedMapper(this);
    }

    public IMapper Mapper { get; }

    public TDestination Map<TSource, TDestination>(TSource? source)
    {
        ThrowIfCompleted<TSource, TDestination>();

        return Resolve<TSource, TDestination>().Create(
            source,
            new MappingContext(MappingOperation.Create, Mapper));
    }

    public TDestination Map<TSource, TDestination>(
        TSource? source,
        TDestination? destination)
    {
        ThrowIfCompleted<TSource, TDestination>();

        return Resolve<TSource, TDestination>().Update(
            source,
            destination,
            new MappingContext(MappingOperation.Update, Mapper));
    }

    public void Complete() => _isCompleted = true;

    private ITypeMapper<TSource, TDestination>
        Resolve<TSource, TDestination>()
    {
        var serviceType =
            typeof(IEnumerable<ITypeMapper<TSource, TDestination>>);
        var service = _serviceProvider.GetService(serviceType);

        if (service is not
            IEnumerable<ITypeMapper<TSource, TDestination>> candidates)
        {
            throw new MappingNotFoundException(
                typeof(TSource),
                typeof(TDestination));
        }

        using var enumerator = candidates.GetEnumerator();

        if (!enumerator.MoveNext())
        {
            throw new MappingNotFoundException(
                typeof(TSource),
                typeof(TDestination));
        }

        var candidate = enumerator.Current;

        if (enumerator.MoveNext())
        {
            throw new AmbiguousMappingException(
                typeof(TSource),
                typeof(TDestination));
        }

        if (candidate is null)
        {
            throw new InvalidMappingRegistrationException(
                typeof(TSource),
                typeof(TDestination));
        }

        return candidate;
    }

    private void ThrowIfCompleted<TSource, TDestination>()
    {
        if (_isCompleted)
        {
            throw new MappingScopeCompletedException(
                typeof(TSource),
                typeof(TDestination));
        }
    }

    private sealed class ScopedMapper : IMapper
    {
        private readonly MappingScope _scope;

        public ScopedMapper(MappingScope scope)
        {
            _scope = scope;
        }

        public TDestination Map<TSource, TDestination>(TSource? source) =>
            _scope.Map<TSource, TDestination>(source);

        public TDestination Map<TSource, TDestination>(
            TSource? source,
            TDestination? destination) =>
            _scope.Map(source, destination);
    }
}
