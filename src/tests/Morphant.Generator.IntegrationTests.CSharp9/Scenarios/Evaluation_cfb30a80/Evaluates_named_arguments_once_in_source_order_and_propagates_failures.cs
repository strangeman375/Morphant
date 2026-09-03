// Compiled integration scenario: TypeMapperNestedMapTests/EvaluationTests::Evaluates_named_arguments_once_in_source_order_and_propagates_failures
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Evaluation_cfb30a80
{
    public sealed record ChildSource(int Value);

    public sealed record ChildDestination(int Value);

    public sealed record OuterSource(ChildSource Child, bool Fail);

    public sealed class OuterDestination
    {
        public ChildDestination Child { get; set; } = new(-1);
    }

    public sealed class TestException : Exception
    {
    }

    public sealed class NestedException : Exception
    {
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper<OuterMapper>
    {
        public static List<string> Events { get; } = new();

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .Members((source, previous) => new()
                {
                    Child = Update<ChildDestination>(
                        destination: GetDestination(previous),
                        source: GetSource(source.Child, source.Fail))
                });

        private static ChildDestination? GetDestination(
            Option<OuterDestination> previous)
        {
            Events.Add("destination");
            return previous.TryGetValue(out var destination)
                ? destination.Child
                : null;
        }

        private static ChildSource GetSource(
            ChildSource source,
            bool fail)
        {
            Events.Add("source");

            if (fail)
            {
                throw new TestException();
            }

            return source;
        }
    }

    public sealed class ChildMapper :
        ITypeMapper<ChildSource, ChildDestination>
    {
        public int Calls { get; private set; }

        public ChildDestination Create(
            ChildSource? source,
            MappingContext context) =>
            throw new InvalidOperationException(
                "The nested Create method was selected.");

        public ChildDestination Update(
            ChildSource? source,
            ChildDestination? destination,
            MappingContext context)
        {
            OuterMapper.Events.Add("map");
            Calls++;

            if (source!.Value == 5)
            {
                throw new NestedException();
            }

            return new ChildDestination(
                source.Value + (destination?.Value ?? 10));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var outer = new OuterMapper();
            var child = new ChildMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<
                    OuterSource,
                    OuterDestination>>(outer)
                .AddSingleton<ITypeMapper<
                    ChildSource,
                    ChildDestination>>(child)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();

            var created = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(new ChildSource(2), Fail: false));
            AssertEvents("create");

            if (created.Child.Value != 12)
            {
                throw new InvalidOperationException(
                    "The nested Create-path result was not assigned.");
            }

            OuterMapper.Events.Clear();
            var previous = new OuterDestination
            {
                Child = new ChildDestination(20)
            };
            var previousChild = previous.Child;
            var updated = mapper.Map(
                new OuterSource(new ChildSource(3), Fail: false),
                previous);
            AssertEvents("update");

            if (!ReferenceEquals(previous, updated) ||
                updated.Child.Value != 23 ||
                ReferenceEquals(previousChild, updated.Child))
            {
                throw new InvalidOperationException(
                    "The authoritative nested replacement was not assigned.");
            }

            OuterMapper.Events.Clear();

            try
            {
                mapper.Map<OuterSource, OuterDestination>(
                    new OuterSource(new ChildSource(4), Fail: true));
                throw new InvalidOperationException(
                    "The argument failure did not propagate.");
            }
            catch (TestException)
            {
            }

            if (OuterMapper.Events.Count != 2 ||
                OuterMapper.Events[0] != "destination" ||
                OuterMapper.Events[1] != "source" ||
                child.Calls != 2)
            {
                throw new InvalidOperationException(
                    "The failing call evaluated an argument more than once " +
                    "or invoked the nested mapper.");
            }

            OuterMapper.Events.Clear();

            try
            {
                mapper.Map<OuterSource, OuterDestination>(
                    new OuterSource(new ChildSource(5), Fail: false));
                throw new InvalidOperationException(
                    "The nested mapping failure did not propagate.");
            }
            catch (NestedException)
            {
            }

            AssertEvents("nested failure");

            if (child.Calls != 3)
            {
                throw new InvalidOperationException(
                    "The failing nested mapper was not invoked once.");
            }
        }

        private static void AssertEvents(string operation)
        {
            var events = OuterMapper.Events;

            if (events.Count != 3 ||
                events[0] != "destination" ||
                events[1] != "source" ||
                events[2] != "map")
            {
                throw new InvalidOperationException(
                    $"The {operation} arguments used the wrong evaluation " +
                    "order.");
            }

            events.Clear();
        }
    }
}
