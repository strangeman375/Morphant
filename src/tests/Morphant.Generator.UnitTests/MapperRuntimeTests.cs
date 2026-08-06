using System.Collections.Concurrent;
using Morphant.Context;

namespace Morphant.Generator.UnitTests;

public sealed class MapperRuntimeTests
{
    [Test]
    public void Requires_a_service_provider()
    {
        Assert.That(
            () => new Mapper(null!),
            Throws.ArgumentNullException.With.Property("ParamName")
                .EqualTo("serviceProvider"));
    }

    [Test]
    public void Dispatches_create_and_update_by_the_exact_registered_pair()
    {
        var calls = new List<MappingCall>();
        var typeMapper = new DelegateTypeMapper<Source, Destination>(
            (source, context) =>
            {
                calls.Add(
                    new MappingCall(
                        context.Operation,
                        context.Mapper,
                        null));

                return new Destination(source?.Value ?? -1);
            },
            (source, destination, context) =>
            {
                calls.Add(
                    new MappingCall(
                        context.Operation,
                        context.Mapper,
                        destination));

                return destination ??
                    new Destination(source?.Value ?? -1);
            });
        var provider = new ManualServiceProvider();
        provider.Add<ITypeMapper<Source, Destination>>(() => typeMapper);
        var mapper = new Mapper(provider);
        var source = new Source(42);

        var created = mapper.Map<Source, Destination>(source);
        var explicitNull = mapper.Map<Source, Destination>(source, null);
        var supplied = new Destination(7);
        var updated = mapper.Map(source, supplied);

        Assert.Multiple(() =>
        {
            Assert.That(created.Value, Is.EqualTo(42));
            Assert.That(explicitNull.Value, Is.EqualTo(42));
            Assert.That(updated, Is.SameAs(supplied));
            Assert.That(
                calls.Select(static call => call.Operation),
                Is.EqualTo(
                    new[]
                    {
                        MappingOperation.Create,
                        MappingOperation.Update,
                        MappingOperation.Update
                    }));
            Assert.That(calls[0].Previous, Is.Null);
            Assert.That(calls[1].Previous, Is.Null);
            Assert.That(calls[2].Previous, Is.SameAs(supplied));
            Assert.That(calls[0].Mapper, Is.Not.SameAs(calls[1].Mapper));
            Assert.That(calls[1].Mapper, Is.Not.SameAs(calls[2].Mapper));
        });
    }

    [Test]
    public void Applies_the_zero_one_or_multiple_candidate_lookup_law()
    {
        var missingMapper = new Mapper(new ManualServiceProvider());
        var emptyProvider = new ManualServiceProvider();
        emptyProvider.Add<ITypeMapper<Source, Destination>>();
        var emptyMapper = new Mapper(emptyProvider);
        var invoked = 0;
        var singleProvider = new ManualServiceProvider();
        singleProvider.Add<ITypeMapper<Source, Destination>>(
            () => CreateConstantMapper(1, () => invoked++));
        var singleMapper = new Mapper(singleProvider);
        var firstProvider = new ManualServiceProvider();
        firstProvider.Add<ITypeMapper<Source, Destination>>(
            () => CreateConstantMapper(1, () => invoked++),
            () => CreateConstantMapper(2, () => invoked++));
        var secondProvider = new ManualServiceProvider();
        secondProvider.Add<ITypeMapper<Source, Destination>>(
            () => CreateConstantMapper(2, () => invoked++),
            () => CreateConstantMapper(1, () => invoked++));

        Assert.Multiple(() =>
        {
            Assert.That(
                () => missingMapper.Map<Source, Destination>(new Source(0)),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => emptyMapper.Map<Source, Destination>(new Source(0)),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                singleMapper.Map<Source, Destination>(new Source(0)).Value,
                Is.EqualTo(1));
            Assert.That(
                () => new Mapper(firstProvider)
                    .Map<Source, Destination>(new Source(0)),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => new Mapper(secondProvider)
                    .Map<Source, Destination>(new Source(0)),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(invoked, Is.EqualTo(1));
        });
    }

    [Test]
    public void Does_not_use_mapping_operation_as_part_of_the_lookup_key()
    {
        var provider = new ManualServiceProvider();
        provider.Add<ITypeMapper<Source, Destination>>(
            () => new DelegateTypeMapper<Source, Destination>(
                (_, _) => new Destination(1),
                (_, _, _) => throw new NotSupportedException()),
            () => new DelegateTypeMapper<Source, Destination>(
                (_, _) => throw new NotSupportedException(),
                (_, destination, _) => destination ?? new Destination(2)));
        var mapper = new Mapper(provider);

        Assert.Multiple(() =>
        {
            Assert.That(
                () => mapper.Map<Source, Destination>(new Source(0)),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => mapper.Map(
                    new Source(0),
                    new Destination(0)),
                Throws.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public void Keeps_nested_frames_in_one_scope_and_completes_it_after_root_map()
    {
        var nestedCalls = new List<MappingCall>();
        IMapper? capturedScopedMapper = null;
        MappingOperation outerOperationAfterNestedCalls = default;
        var childMapper = new DelegateTypeMapper<ChildSource, ChildDestination>(
            (source, context) =>
            {
                nestedCalls.Add(
                    new MappingCall(
                        context.Operation,
                        context.Mapper,
                        null));

                return new ChildDestination(source?.Value ?? -1);
            },
            (source, destination, context) =>
            {
                nestedCalls.Add(
                    new MappingCall(
                        context.Operation,
                        context.Mapper,
                        destination));

                return destination ??
                    new ChildDestination(source?.Value ?? -1);
            });
        var outerMapper = new DelegateTypeMapper<Source, Destination>(
            (source, context) =>
            {
                capturedScopedMapper = context.Mapper;
                var childSource = new ChildSource(source?.Value ?? -1);
                var created = context.Mapper
                    .Map<ChildSource, ChildDestination>(childSource);
                var explicitNull = context.Mapper
                    .Map<ChildSource, ChildDestination>(childSource, null);
                var supplied = new ChildDestination(3);
                var updated = context.Mapper.Map(childSource, supplied);
                outerOperationAfterNestedCalls = context.Operation;

                return new Destination(
                    created.Value + explicitNull.Value + updated.Value);
            },
            (_, destination, _) => destination ?? new Destination(-1));
        var provider = new ManualServiceProvider();
        provider.Add<ITypeMapper<Source, Destination>>(() => outerMapper);
        provider.Add<ITypeMapper<ChildSource, ChildDestination>>(
            () => childMapper);
        var result = new Mapper(provider)
            .Map<Source, Destination>(new Source(5));

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo(13));
            Assert.That(
                nestedCalls.Select(static call => call.Operation),
                Is.EqualTo(
                    new[]
                    {
                        MappingOperation.Create,
                        MappingOperation.Update,
                        MappingOperation.Update
                    }));
            Assert.That(nestedCalls[0].Previous, Is.Null);
            Assert.That(nestedCalls[1].Previous, Is.Null);
            Assert.That(nestedCalls[2].Previous, Is.Not.Null);
            Assert.That(
                nestedCalls.All(call =>
                    ReferenceEquals(call.Mapper, capturedScopedMapper)),
                Is.True);
            Assert.That(
                outerOperationAfterNestedCalls,
                Is.EqualTo(MappingOperation.Create));
            Assert.That(
                () => capturedScopedMapper!
                    .Map<ChildSource, ChildDestination>(new ChildSource(1)),
                Throws.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public void Supports_recursion_reentrancy_and_caught_nested_exceptions()
    {
        var scopedMappers = new HashSet<IMapper>();
        var provider = new ManualServiceProvider();
        provider.Add<ITypeMapper<int, int>>(
            () => new DelegateTypeMapper<int, int>(
                (source, context) =>
                {
                    scopedMappers.Add(context.Mapper);

                    return source == 0
                        ? 0
                        : context.Mapper.Map<int, int>(source - 1) + 1;
                },
                (_, destination, _) => destination));
        provider.Add<ITypeMapper<FailingSource, Destination>>(
            () => new DelegateTypeMapper<FailingSource, Destination>(
                (_, _) => throw new TestException(),
                (_, _, _) => throw new TestException()));
        provider.Add<ITypeMapper<RecoverySource, Destination>>(
            () => new DelegateTypeMapper<RecoverySource, Destination>(
                (_, context) =>
                {
                    try
                    {
                        context.Mapper.Map<FailingSource, Destination>(
                            new FailingSource());
                    }
                    catch (TestException)
                    {
                        return new Destination(
                            context.Mapper.Map<int, int>(3));
                    }

                    throw new AssertionException(
                        "The nested failure was not observed.");
                },
                (_, destination, _) => destination ?? new Destination(-1)));
        var mapper = new Mapper(provider);

        var recursiveResult = mapper.Map<int, int>(5);
        var recovered = mapper.Map<RecoverySource, Destination>(
            new RecoverySource());

        Assert.Multiple(() =>
        {
            Assert.That(recursiveResult, Is.EqualTo(5));
            Assert.That(recovered.Value, Is.EqualTo(3));
            Assert.That(scopedMappers, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Completes_the_scope_when_the_root_mapping_throws()
    {
        IMapper? capturedScopedMapper = null;
        var provider = new ManualServiceProvider();
        provider.Add<ITypeMapper<FailingSource, Destination>>(
            () => new DelegateTypeMapper<FailingSource, Destination>(
                (_, context) =>
                {
                    capturedScopedMapper = context.Mapper;
                    throw new TestException();
                },
                (_, _, _) => throw new TestException()));
        var mapper = new Mapper(provider);

        Assert.That(
            () => mapper.Map<FailingSource, Destination>(new FailingSource()),
            Throws.TypeOf<TestException>());
        Assert.That(
            () => capturedScopedMapper!
                .Map<FailingSource, Destination>(new FailingSource()),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public async Task Creates_independent_scopes_for_parallel_root_calls()
    {
        var scopedMappers = new ConcurrentBag<IMapper>();
        using var barrier = new Barrier(2);
        var provider = new ManualServiceProvider();
        provider.Add<ITypeMapper<Source, Destination>>(
            () => new DelegateTypeMapper<Source, Destination>(
                (source, context) =>
                {
                    scopedMappers.Add(context.Mapper);
                    barrier.SignalAndWait();

                    return new Destination(source?.Value ?? -1);
                },
                (_, destination, _) => destination ?? new Destination(-1)));
        var mapper = new Mapper(provider);

        var results = await Task.WhenAll(
            Task.Run(() => mapper.Map<Source, Destination>(new Source(1))),
            Task.Run(() => mapper.Map<Source, Destination>(new Source(2))));

        Assert.Multiple(() =>
        {
            Assert.That(
                results.Select(static result => result.Value),
                Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(scopedMappers, Has.Count.EqualTo(2));
            Assert.That(
                scopedMappers.Distinct().Count(),
                Is.EqualTo(2));
        });
    }

    [Test]
    public void Resolves_transient_mappers_from_the_same_application_provider()
    {
        var dependency = new ScopedDependency();
        var activations = new List<Activation>();
        var provider = new ManualServiceProvider();
        provider.Add<ITypeMapper<Source, Destination>>(
            () =>
            {
                var activation = new Activation(dependency);
                activations.Add(activation);

                return new DelegateTypeMapper<Source, Destination>(
                    (source, context) =>
                    {
                        var nested = context.Mapper
                            .Map<ChildSource, ChildDestination>(
                                new ChildSource(source?.Value ?? -1));

                        return new Destination(nested.Value);
                    },
                    (_, destination, _) =>
                        destination ?? new Destination(-1));
            });
        provider.Add<ITypeMapper<ChildSource, ChildDestination>>(
            () =>
            {
                var activation = new Activation(dependency);
                activations.Add(activation);

                return new DelegateTypeMapper<ChildSource, ChildDestination>(
                    (source, _) =>
                        new ChildDestination(source?.Value ?? -1),
                    (_, destination, _) =>
                        destination ?? new ChildDestination(-1));
            });
        var result = new Mapper(provider)
            .Map<Source, Destination>(new Source(8));

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo(8));
            Assert.That(activations, Has.Count.EqualTo(2));
            Assert.That(activations[0], Is.Not.SameAs(activations[1]));
            Assert.That(
                activations.All(activation =>
                    ReferenceEquals(activation.Dependency, dependency)),
                Is.True);
        });
    }

    [Test]
    public void Uses_closed_generic_and_nullable_types_in_the_exact_pair()
    {
        var provider = new ManualServiceProvider();
        provider.Add<ITypeMapper<int?, Box<int?>>>(
            () => new DelegateTypeMapper<int?, Box<int?>>(
                (source, _) => new Box<int?>(source),
                (_, destination, _) => destination ?? new Box<int?>(null)));
        provider.Add<ITypeMapper<GenericSource<string>, GenericDestination<int>>>(
            () => new DelegateTypeMapper<
                GenericSource<string>,
                GenericDestination<int>>(
                (source, _) =>
                    new GenericDestination<int>(source?.Value.Length ?? -1),
                (_, destination, _) =>
                    destination ?? new GenericDestination<int>(-1)));
        var mapper = new Mapper(provider);

        var nullableResult = mapper.Map<int?, Box<int?>>(null);
        var genericResult = mapper.Map<
            GenericSource<string>,
            GenericDestination<int>>(new GenericSource<string>("four"));

        Assert.Multiple(() =>
        {
            Assert.That(nullableResult.Value, Is.Null);
            Assert.That(genericResult.Value, Is.EqualTo(4));
            Assert.That(
                () => mapper.Map<
                    GenericSource<int>,
                    GenericDestination<int>>(new GenericSource<int>(1)),
                Throws.TypeOf<InvalidOperationException>());
        });
    }

    private static DelegateTypeMapper<Source, Destination>
        CreateConstantMapper(int value, Action onMap) =>
        new(
            (_, _) =>
            {
                onMap();
                return new Destination(value);
            },
            (_, destination, _) => destination ?? new Destination(value));

    private sealed class ManualServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, Func<object>> _services = new();

        public object? GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var factory)
                ? factory()
                : null;

        public void Add<TService>(params Func<TService>[] factories)
            where TService : class
        {
            _services[typeof(IEnumerable<TService>)] = () =>
                factories.Select(static factory => factory()).ToArray();
        }
    }

    private sealed class DelegateTypeMapper<TSource, TDestination> :
        ITypeMapper<TSource, TDestination>
    {
        private readonly Func<
            TSource?,
            MappingContext,
            TDestination> _create;
        private readonly Func<
            TSource?,
            TDestination?,
            MappingContext,
            TDestination> _update;

        public DelegateTypeMapper(
            Func<TSource?, MappingContext, TDestination> create,
            Func<
                TSource?,
                TDestination?,
                MappingContext,
                TDestination> update)
        {
            _create = create;
            _update = update;
        }

        public TDestination Map(TSource? source, MappingContext context) =>
            _create(source, context);

        public TDestination Map(
            TSource? source,
            TDestination? destination,
            MappingContext context) =>
            _update(source, destination, context);
    }

    private sealed record Source(int Value);

    private sealed record Destination(int Value);

    private sealed record ChildSource(int Value);

    private sealed record ChildDestination(int Value);

    private sealed record GenericSource<T>(T Value);

    private sealed record GenericDestination<T>(T Value);

    private sealed record Box<T>(T Value);

    private sealed class FailingSource;

    private sealed class RecoverySource;

    private sealed class ScopedDependency;

    private sealed record Activation(ScopedDependency Dependency);

    private sealed record MappingCall(
        MappingOperation Operation,
        IMapper Mapper,
        object? Previous);

    private sealed class TestException : Exception;
}
