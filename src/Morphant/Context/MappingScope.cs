using System.Collections.Concurrent;
using Morphant.Exceptions;

namespace Morphant.Context;

internal sealed class MappingScope
{
    private static readonly ConcurrentDictionary<Type, HashSet<Type>>
        StandaloneContracts = new();

    private readonly IServiceProvider? _serviceProvider;
    private readonly object? _standaloneMapper;
    private readonly HashSet<Type>? _standaloneContracts;
    private bool _isCompleted;

    public MappingScope(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Mapper = new ScopedMapper(this);
    }

    private MappingScope(object standaloneMapper)
    {
        _standaloneMapper = standaloneMapper;
        _standaloneContracts = StandaloneContracts.GetOrAdd(
            standaloneMapper.GetType(),
            static type => new HashSet<Type>(
                type.GetInterfaces().Where(static contract =>
                    contract.IsGenericType &&
                    contract.GetGenericTypeDefinition() ==
                    typeof(ITypeMapper<,>))));
        Mapper = new ScopedMapper(this);
    }

    public static MappingScope CreateStandalone(object mapper) =>
        new(mapper);

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
        if (_standaloneMapper is not null)
        {
            if (_standaloneContracts!.Contains(
                    typeof(ITypeMapper<TSource, TDestination>)))
            {
                return (ITypeMapper<TSource, TDestination>)
                    _standaloneMapper;
            }

            throw MappingNotFoundException.ForStandalone(
                operation,
                typeof(TSource),
                typeof(TDestination));
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
