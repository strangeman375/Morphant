// Compiled integration scenario: MapperDispatchTests/ScopeTests::Keeps_nested_calls_in_scope_and_completes_it_after_success_or_failure
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ApplicationScope_9d7a0103
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
        public ChildSource Child { get; init; } = new ChildSource();

        public bool Fail { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; init; }
    }

    public sealed class ScenarioException : Exception
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static IMapper? CapturedMapper { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<int, int>()
                .Convert((source, _, context) => source == 0
                    ? 0
                    : context.Mapper.Map<int, int>(source - 1) + 1);
            builder.Map<ChildSource, ChildDestination>();
            builder.Map<Source, Destination>()
                .Convert((source, _, context) =>
                {
                    CapturedMapper = context.Mapper;
                    var child = context.Mapper.Map<
                        ChildSource,
                        ChildDestination>(source!.Child);

                    if (source.Fail)
                    {
                        throw new ScenarioException();
                    }

                    return new Destination { Value = child.Value };
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var generated = new TestMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<Source, Destination>>(generated)
                .AddSingleton<ITypeMapper<
                    ChildSource,
                    ChildDestination>>(generated)
                .AddSingleton<ITypeMapper<int, int>>(generated)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();
            var recursive = mapper.Map<int, int>(5);
            var result = mapper.Map<Source, Destination>(new Source
            {
                Child = new ChildSource { Value = 8 }
            });
            var successfulScope = TestMapper.CapturedMapper;

            if (recursive != 5 ||
                result.Value != 8 ||
                successfulScope is null)
            {
                throw new InvalidOperationException(
                    "The nested call did not use the application scope.");
            }

            ExpectCompleted(successfulScope);

            try
            {
                mapper.Map<Source, Destination>(new Source
                {
                    Fail = true
                });
                throw new InvalidOperationException(
                    "The user callback failure was not propagated.");
            }
            catch (ScenarioException)
            {
            }

            var failedScope = TestMapper.CapturedMapper;

            if (failedScope is null || ReferenceEquals(
                    successfulScope,
                    failedScope))
            {
                throw new InvalidOperationException(
                    "Each root mapping call must receive a new scope.");
            }

            ExpectCompleted(failedScope);

            var next = mapper.Map<Source, Destination>(new Source
            {
                Child = new ChildSource { Value = 13 }
            });

            if (next.Value != 13)
            {
                throw new InvalidOperationException(
                    "A failed root call affected the next mapping scope.");
            }

            VerifyDefaultContext();
        }

        private static void ExpectCompleted(IMapper mapper)
        {
            try
            {
                mapper.Map<ChildSource, ChildDestination>(
                    new ChildSource());
            }
            catch (MappingScopeCompletedException exception)
                when (exception.Operation == MappingOperation.Create &&
                      exception.SourceType == typeof(ChildSource) &&
                      exception.DestinationType ==
                      typeof(ChildDestination))
            {
                return;
            }

            throw new InvalidOperationException(
                "context.Mapper remained usable after the root call.");
        }

        private static void VerifyDefaultContext()
        {
            var context = default(MappingContext);
            var operationFailed = false;
            var mapperFailed = false;

            try
            {
                _ = context.Operation;
            }
            catch (InvalidMappingContextException)
            {
                operationFailed = true;
            }

            try
            {
                _ = context.Mapper;
            }
            catch (InvalidMappingContextException)
            {
                mapperFailed = true;
            }

            if (!operationFailed || !mapperFailed)
            {
                throw new InvalidOperationException(
                    "A default MappingContext exposed initialized data.");
            }
        }
    }
}
