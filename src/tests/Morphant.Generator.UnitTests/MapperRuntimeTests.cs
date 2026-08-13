using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.UnitTests;

public sealed class MapperRuntimeTests
{
    [Test]
    public void Rejects_a_null_service_provider_as_an_argument_error()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new Mapper(null!));

        Assert.That(exception!.ParamName, Is.EqualTo("serviceProvider"));
    }

    [Test]
    public void Rejects_default_context_only_when_its_data_is_read()
    {
        var context = default(MappingContext);
        var mapper = new DelegateTypeMapper<Source, Destination>(
            (source, _) => new Destination(source?.Value ?? -1),
            (_, destination, _) => destination ?? new Destination(-1));

        var result = ((ITypeMapper<Source, Destination>)mapper).Create(
            new Source(4),
            context);

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo(4));
            Assert.That(
                () => _ = context.Operation,
                Throws.TypeOf<InvalidMappingContextException>());
            Assert.That(
                () => _ = context.Mapper,
                Throws.TypeOf<InvalidMappingContextException>());
        });
    }

    [Test]
    public void Invokes_single_and_multi_pair_mappers_without_a_root_mapper()
    {
        var single = new DelegateTypeMapper<Source, Destination>(
            (source, _) => new Destination(source?.Value ?? -1),
            (_, destination, _) => destination ?? new Destination(-1));
        var multi = new StandaloneTypeMapper();

        var singleResult = single.Create(new Source(2));
        var created = multi.Create<Source, Destination>(new Source(5));
        var supplied = new Destination(7);
        var updated = multi.Update(new Source(9), supplied);

        Assert.Multiple(() =>
        {
            Assert.That(singleResult.Value, Is.EqualTo(2));
            Assert.That(created.Value, Is.EqualTo(10));
            Assert.That(updated, Is.SameAs(supplied));
            Assert.That(multi.Operations, Is.EqualTo(new[]
            {
                MappingOperation.Create,
                MappingOperation.Update
            }));
            Assert.That(
                () => multi.CapturedMapper!
                    .Map<ChildSource, ChildDestination>(new ChildSource(1)),
                Throws.TypeOf<MappingScopeCompletedException>());
        });
    }

    [Test]
    public void Explains_the_boundary_of_a_standalone_mapper_scope()
    {
        var mapper = new StandaloneTypeMapper();

        var exception = Assert.Throws<MappingNotFoundException>(() =>
            mapper.Create<Source, Destination>(new Source(-1)));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Operation, Is.EqualTo(MappingOperation.Create));
            Assert.That(exception.SourceType, Is.EqualTo(typeof(FailingSource)));
            Assert.That(exception.DestinationType, Is.EqualTo(typeof(Destination)));
            Assert.That(
                exception.Message,
                Does.Contain("Use IMapper"));
            Assert.That(
                () => mapper.CapturedMapper!
                    .Map<Source, Destination>(new Source(1)),
                Throws.TypeOf<MappingScopeCompletedException>());
        });
    }

    [Test]
    public void Uses_a_contravariant_root_capability_without_widening_nested_lookup()
    {
        var mapper = new ContravariantTypeMapper();
        ITypeMapper<DerivedSource, Destination> capability = mapper;

        var result = capability.Create(new DerivedSource());

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo(1));
            Assert.That(mapper.NestedException, Is.Not.Null);
            Assert.That(
                mapper.NestedException!.SourceType,
                Is.EqualTo(typeof(DerivedSource)));
            Assert.That(
                mapper.NestedException.DestinationType,
                Is.EqualTo(typeof(Destination)));
            Assert.That(mapper.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Keeps_the_selected_pair_available_to_a_manual_mapper()
    {
        var callCount = 0;
        var mapper = new DelegateTypeMapper<int, int>(
            (source, context) =>
            {
                callCount++;

                return source == 0
                    ? 0
                    : context.Mapper.Map<int, int>(source - 1) + 1;
            },
            (_, destination, _) => destination);

        var result = mapper.Create(4);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(4));
            Assert.That(callCount, Is.EqualTo(5));
        });
    }

    [Test]
    public void Rejects_a_null_context_free_type_mapper()
    {
        ITypeMapper<Source, Destination>? mapper = null;

        var createException = Assert.Throws<ArgumentNullException>(() =>
            mapper!.Create(new Source(1)));
        var updateException = Assert.Throws<ArgumentNullException>(() =>
            mapper!.Update(new Source(1), new Destination(1)));

        Assert.Multiple(() =>
        {
            Assert.That(createException!.ParamName, Is.EqualTo("mapper"));
            Assert.That(updateException!.ParamName, Is.EqualTo("mapper"));
        });
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
        using var provider = new ServiceCollection()
            .AddSingleton<ITypeMapper<Source, Destination>>(typeMapper)
            .BuildServiceProvider();
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
        using var emptyProvider = new ServiceCollection()
            .BuildServiceProvider();
        var emptyMapper = new Mapper(emptyProvider);
        var invoked = 0;
        using var singleProvider = new ServiceCollection()
            .AddTransient<ITypeMapper<Source, Destination>>(_ =>
                CreateConstantMapper(1, () => invoked++))
            .BuildServiceProvider();
        var singleMapper = new Mapper(singleProvider);
        using var firstProvider = new ServiceCollection()
            .AddTransient<ITypeMapper<Source, Destination>>(_ =>
                CreateConstantMapper(1, () => invoked++))
            .AddTransient<ITypeMapper<Source, Destination>>(_ =>
                CreateConstantMapper(2, () => invoked++))
            .BuildServiceProvider();
        using var secondProvider = new ServiceCollection()
            .AddTransient<ITypeMapper<Source, Destination>>(_ =>
                CreateConstantMapper(2, () => invoked++))
            .AddTransient<ITypeMapper<Source, Destination>>(_ =>
                CreateConstantMapper(1, () => invoked++))
            .BuildServiceProvider();

        Assert.Multiple(() =>
        {
            Assert.That(
                () => emptyMapper.Map<Source, Destination>(new Source(0)),
                Throws.TypeOf<MappingNotFoundException>());
            Assert.That(
                singleMapper.Map<Source, Destination>(new Source(0)).Value,
                Is.EqualTo(1));
            Assert.That(
                () => new Mapper(firstProvider)
                    .Map<Source, Destination>(new Source(0)),
                Throws.TypeOf<AmbiguousMappingException>());
            Assert.That(
                () => new Mapper(secondProvider)
                    .Map<Source, Destination>(new Source(0)),
                Throws.TypeOf<AmbiguousMappingException>());
            Assert.That(invoked, Is.EqualTo(1));
        });
    }

    [Test]
    public void Does_not_use_mapping_operation_as_part_of_the_lookup_key()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<ITypeMapper<Source, Destination>>(
                new DelegateTypeMapper<Source, Destination>(
                    (_, _) => new Destination(1),
                    (_, _, _) => throw new NotSupportedException()))
            .AddSingleton<ITypeMapper<Source, Destination>>(
                new DelegateTypeMapper<Source, Destination>(
                    (_, _) => throw new NotSupportedException(),
                    (_, destination, _) =>
                        destination ?? new Destination(2)))
            .BuildServiceProvider();
        var mapper = new Mapper(provider);

        Assert.Multiple(() =>
        {
            Assert.That(
                () => mapper.Map<Source, Destination>(new Source(0)),
                Throws.TypeOf<AmbiguousMappingException>());
            Assert.That(
                () => mapper.Map(
                    new Source(0),
                    new Destination(0)),
                Throws.TypeOf<AmbiguousMappingException>());
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
        using var provider = new ServiceCollection()
            .AddSingleton<ITypeMapper<Source, Destination>>(outerMapper)
            .AddSingleton<ITypeMapper<
                ChildSource,
                ChildDestination>>(childMapper)
            .BuildServiceProvider();
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
                Throws.TypeOf<MappingScopeCompletedException>());
        });
    }

    [Test]
    public void Supports_recursion_reentrancy_and_caught_nested_exceptions()
    {
        var scopedMappers = new HashSet<IMapper>();
        using var provider = new ServiceCollection()
            .AddSingleton<ITypeMapper<int, int>>(
                new DelegateTypeMapper<int, int>(
                    (source, context) =>
                    {
                        scopedMappers.Add(context.Mapper);

                        return source == 0
                            ? 0
                            : context.Mapper.Map<int, int>(source - 1) + 1;
                    },
                    (_, destination, _) => destination))
            .AddSingleton<ITypeMapper<FailingSource, Destination>>(
                new DelegateTypeMapper<FailingSource, Destination>(
                    (_, _) => throw new TestException(),
                    (_, _, _) => throw new TestException()))
            .AddSingleton<ITypeMapper<RecoverySource, Destination>>(
                new DelegateTypeMapper<RecoverySource, Destination>(
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
                    (_, destination, _) =>
                        destination ?? new Destination(-1)))
            .BuildServiceProvider();
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
        using var provider = new ServiceCollection()
            .AddSingleton<ITypeMapper<FailingSource, Destination>>(
                new DelegateTypeMapper<FailingSource, Destination>(
                    (_, context) =>
                    {
                        capturedScopedMapper = context.Mapper;
                        throw new TestException();
                    },
                    (_, _, _) => throw new TestException()))
            .BuildServiceProvider();
        var mapper = new Mapper(provider);

        Assert.That(
            () => mapper.Map<FailingSource, Destination>(new FailingSource()),
            Throws.TypeOf<TestException>());
        Assert.That(
            () => capturedScopedMapper!
                .Map<FailingSource, Destination>(new FailingSource()),
            Throws.TypeOf<MappingScopeCompletedException>());
    }

    [Test]
    public async Task Creates_independent_scopes_for_parallel_root_calls()
    {
        var scopedMappers = new ConcurrentBag<IMapper>();
        using var barrier = new Barrier(2);
        using var provider = new ServiceCollection()
            .AddSingleton<ITypeMapper<Source, Destination>>(
                new DelegateTypeMapper<Source, Destination>(
                    (source, context) =>
                    {
                        scopedMappers.Add(context.Mapper);
                        barrier.SignalAndWait();

                        return new Destination(source?.Value ?? -1);
                    },
                    (_, destination, _) =>
                        destination ?? new Destination(-1)))
            .BuildServiceProvider();
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
        var dependency = new SharedDependency();
        var activations = new List<Activation>();
        using var provider = new ServiceCollection()
            .AddSingleton(dependency)
            .AddTransient<ITypeMapper<Source, Destination>>(
                serviceProvider =>
                {
                    var activation = new Activation(
                        serviceProvider
                            .GetRequiredService<SharedDependency>());
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
                })
            .AddTransient<ITypeMapper<ChildSource, ChildDestination>>(
                serviceProvider =>
                {
                    var activation = new Activation(
                        serviceProvider
                            .GetRequiredService<SharedDependency>());
                    activations.Add(activation);

                    return new DelegateTypeMapper<
                        ChildSource,
                        ChildDestination>(
                        (source, _) =>
                            new ChildDestination(source?.Value ?? -1),
                        (_, destination, _) =>
                            destination ?? new ChildDestination(-1));
                })
            .BuildServiceProvider();
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
        using var provider = new ServiceCollection()
            .AddSingleton<ITypeMapper<int?, Box<int?>>>(
                new DelegateTypeMapper<int?, Box<int?>>(
                    (source, _) => new Box<int?>(source),
                    (_, destination, _) =>
                        destination ?? new Box<int?>(null)))
            .AddSingleton<ITypeMapper<
                GenericSource<string>,
                GenericDestination<int>>>(
                new DelegateTypeMapper<
                    GenericSource<string>,
                    GenericDestination<int>>(
                    (source, _) => new GenericDestination<int>(
                        source?.Value.Length ?? -1),
                    (_, destination, _) =>
                        destination ?? new GenericDestination<int>(-1)))
            .BuildServiceProvider();
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
                Throws.TypeOf<MappingNotFoundException>());
        });
    }

    [Test]
    public void Rejects_a_registration_that_resolves_to_null()
    {
        using var provider = new ServiceCollection()
            .AddTransient<ITypeMapper<Source, Destination>>(_ => null!)
            .BuildServiceProvider();
        using var ambiguousProvider = new ServiceCollection()
            .AddTransient<ITypeMapper<Source, Destination>>(_ => null!)
            .AddSingleton<ITypeMapper<Source, Destination>>(
                CreateConstantMapper(1, static () => { }))
            .BuildServiceProvider();
        var mapper = new Mapper(provider);

        Assert.Multiple(() =>
        {
            Assert.That(
                () => mapper.Map<Source, Destination>(new Source(0)),
                Throws.TypeOf<InvalidMappingRegistrationException>());
            Assert.That(
                () => new Mapper(ambiguousProvider)
                    .Map<Source, Destination>(new Source(0)),
                Throws.TypeOf<AmbiguousMappingException>());
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

        public TDestination Create(TSource? source, MappingContext context) =>
            _create(source, context);

        public TDestination Update(
            TSource? source,
            TDestination? destination,
            MappingContext context) =>
            _update(source, destination, context);
    }

    private sealed class StandaloneTypeMapper : TypeMapper,
        ITypeMapper<Source, Destination>,
        ITypeMapper<ChildSource, ChildDestination>
    {
        public List<MappingOperation> Operations { get; } = new();

        public IMapper? CapturedMapper { get; private set; }

        protected override bool Supports(
            Type sourceType,
            Type destinationType) =>
            sourceType == typeof(Source) &&
            destinationType == typeof(Destination) ||
            sourceType == typeof(ChildSource) &&
            destinationType == typeof(ChildDestination) ||
            base.Supports(sourceType, destinationType);

        protected override void Configure(MapperBuilder builder)
        {
        }

        Destination ITypeMapper<Source, Destination>.Create(
            Source? source,
            MappingContext context)
        {
            Operations.Add(context.Operation);
            CapturedMapper = context.Mapper;

            if (source?.Value < 0)
            {
                return context.Mapper.Map<FailingSource, Destination>(
                    new FailingSource());
            }

            var child = context.Mapper.Map<
                ChildSource,
                ChildDestination>(new ChildSource(source?.Value ?? -1));

            return new Destination((source?.Value ?? -1) + child.Value);
        }

        Destination ITypeMapper<Source, Destination>.Update(
            Source? source,
            Destination? destination,
            MappingContext context)
        {
            Operations.Add(context.Operation);
            CapturedMapper = context.Mapper;

            return destination ?? new Destination(source?.Value ?? -1);
        }

        ChildDestination ITypeMapper<ChildSource, ChildDestination>.Create(
            ChildSource? source,
            MappingContext context)
        {
            Assert.That(context.Mapper, Is.SameAs(CapturedMapper));
            return new ChildDestination(source?.Value ?? -1);
        }

        ChildDestination ITypeMapper<ChildSource, ChildDestination>.Update(
            ChildSource? source,
            ChildDestination? destination,
            MappingContext context) =>
            destination ?? new ChildDestination(source?.Value ?? -1);
    }

    private sealed class ContravariantTypeMapper : TypeMapper,
        ITypeMapper<BaseSource, Destination>
    {
        public int CallCount { get; private set; }

        public MappingNotFoundException? NestedException { get; private set; }

        protected override bool Supports(
            Type sourceType,
            Type destinationType) =>
            sourceType == typeof(BaseSource) &&
            destinationType == typeof(Destination) ||
            base.Supports(sourceType, destinationType);

        protected override void Configure(MapperBuilder builder)
        {
        }

        public Destination Create(
            BaseSource? source,
            MappingContext context)
        {
            CallCount++;

            try
            {
                context.Mapper.Map<DerivedSource, Destination>(
                    new DerivedSource());
            }
            catch (MappingNotFoundException exception)
            {
                NestedException = exception;
            }

            return new Destination(1);
        }

        public Destination Update(
            BaseSource? source,
            Destination? destination,
            MappingContext context)
        {
            CallCount++;
            return destination ?? new Destination(1);
        }
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

    private class BaseSource;

    private sealed class DerivedSource : BaseSource;

    private sealed class SharedDependency;

    private sealed record Activation(SharedDependency Dependency);

    private sealed record MappingCall(
        MappingOperation Operation,
        IMapper Mapper,
        object? Previous);

    private sealed class TestException : Exception;
}
