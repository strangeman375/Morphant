using Morphant.Exceptions;

namespace Morphant.Context;

internal sealed class MappingScope
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly object? _standaloneMapper;
    private readonly Type? _standaloneSourceType;
    private readonly Type? _standaloneDestinationType;
    private bool _isCompleted;

    public MappingScope(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Mapper = new ScopedMapper(this);
    }

    private MappingScope(
        object standaloneMapper,
        Type sourceType,
        Type destinationType)
    {
        _standaloneMapper = standaloneMapper;
        _standaloneSourceType = sourceType;
        _standaloneDestinationType = destinationType;
        Mapper = new ScopedMapper(this);
    }

    public static MappingScope CreateStandalone<TSource, TDestination>(
        ITypeMapper<TSource, TDestination> mapper) =>
        new(
            mapper,
            typeof(TSource),
            typeof(TDestination));

    public IMapper Mapper { get; }

    public TDestination Map<TSource, TDestination>(TSource? source)
    {
        const MappingOperation operation = MappingOperation.Create;
        ThrowIfCompleted<TSource, TDestination>(operation);

        return Resolve<TSource, TDestination>(operation).Create(
            source,
            new MappingContext(operation, Mapper));
    }

    public TDestination Map<TSource, TDestination>(
        TSource? source,
        TDestination? destination)
    {
        const MappingOperation operation = MappingOperation.Update;
        ThrowIfCompleted<TSource, TDestination>(operation);

        return Resolve<TSource, TDestination>(operation).Update(
            source,
            destination,
            new MappingContext(operation, Mapper));
    }

    public void Complete() => _isCompleted = true;

    private ITypeMapper<TSource, TDestination>
        Resolve<TSource, TDestination>(MappingOperation operation)
    {
        if (_standaloneMapper is { } standaloneMapper)
        {
            var sourceType = typeof(TSource);
            var destinationType = typeof(TDestination);
            var isDeclared = standaloneMapper is TypeMapper typeMapper
                ? typeMapper.Supports(sourceType, destinationType)
                : sourceType == _standaloneSourceType &&
                  destinationType == _standaloneDestinationType;

            if (isDeclared &&
                standaloneMapper is
                    ITypeMapper<TSource, TDestination> mapper)
            {
                return mapper;
            }

            throw MappingNotFoundException.ForStandalone(
                operation,
                sourceType,
                destinationType);
        }

        var serviceType =
            typeof(IEnumerable<ITypeMapper<TSource, TDestination>>);
        var service = _serviceProvider!.GetService(serviceType);

        if (service is not
            IEnumerable<ITypeMapper<TSource, TDestination>> candidates)
        {
            throw new MappingNotFoundException(
                operation,
                typeof(TSource),
                typeof(TDestination));
        }

        using var enumerator = candidates.GetEnumerator();

        if (!enumerator.MoveNext())
        {
            throw new MappingNotFoundException(
                operation,
                typeof(TSource),
                typeof(TDestination));
        }

        var candidate = enumerator.Current;

        if (enumerator.MoveNext())
        {
            throw new AmbiguousMappingException(
                operation,
                typeof(TSource),
                typeof(TDestination));
        }

        if (candidate is null)
        {
            throw new InvalidMappingRegistrationException(
                operation,
                typeof(TSource),
                typeof(TDestination));
        }

        return candidate;
    }

    private void ThrowIfCompleted<TSource, TDestination>(
        MappingOperation operation)
    {
        if (_isCompleted)
        {
            throw new MappingScopeCompletedException(
                operation,
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
