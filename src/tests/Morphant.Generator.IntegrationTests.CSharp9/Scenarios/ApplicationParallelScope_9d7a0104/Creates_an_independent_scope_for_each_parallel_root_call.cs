// Compiled integration scenario: MapperDispatchTests/ScopeTests::Creates_an_independent_scope_for_each_parallel_root_call
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ApplicationParallelScope_9d7a0104
{
    public sealed class ChildSource
    {
        public int Value { get; init; }
    }

    public sealed class ChildDestination
    {
        public int Value { get; set; }
    }

    public sealed class Source
    {
        public int Id { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static ConcurrentDictionary<int, IMapper> CapturedMappers
            { get; } = new();

        public static Barrier? Barrier { get; set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>();
            builder.Map<Source, Destination>()
                .Convert((source, _, context) =>
                {
                    CapturedMappers[source!.Id] = context.Mapper;
                    var barrier = Barrier ??
                        throw new InvalidOperationException(
                            "The scenario barrier was not initialized.");

                    if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10)))
                    {
                        throw new TimeoutException(
                            "Parallel mapping calls did not overlap.");
                    }

                    var child = context.Mapper.Map<
                        ChildSource,
                        ChildDestination>(new ChildSource
                        {
                            Value = source.Id
                        });
                    return new Destination { Value = child.Value };
                });
        }
    }

    public static class Scenario
    {
        public static async Task Verify()
        {
            TestMapper.CapturedMappers.Clear();
            using var barrier = new Barrier(2);
            TestMapper.Barrier = barrier;
            var generated = new TestMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<Source, Destination>>(generated)
                .AddSingleton<ITypeMapper<
                    ChildSource,
                    ChildDestination>>(generated)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();

            try
            {
                var results = await Task.WhenAll(
                    Task.Run(() => mapper.Map<Source, Destination>(
                        new Source { Id = 1 })),
                    Task.Run(() => mapper.Map<Source, Destination>(
                        new Source { Id = 2 })));

                if (!results.Select(result => result.Value)
                        .OrderBy(value => value)
                        .SequenceEqual(new[] { 1, 2 }) ||
                    TestMapper.CapturedMappers.Count != 2 ||
                    TestMapper.CapturedMappers.Values.Distinct().Count() != 2)
                {
                    throw new InvalidOperationException(
                        "Parallel root calls shared mapping state.");
                }

                foreach (var scopedMapper in
                         TestMapper.CapturedMappers.Values)
                {
                    try
                    {
                        scopedMapper.Map<ChildSource, ChildDestination>(
                            new ChildSource());
                    }
                    catch (MappingScopeCompletedException)
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        "A parallel mapping scope remained active.");
                }
            }
            finally
            {
                TestMapper.Barrier = null;
            }
        }
    }
}
