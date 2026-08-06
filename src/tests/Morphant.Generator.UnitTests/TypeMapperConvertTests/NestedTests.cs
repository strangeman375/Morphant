using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperConvertTests;

[TestFixture]
internal sealed class NestedTests
{
    [Test]
    public void Uses_the_scoped_mapper_and_keeps_outer_frames_immutable()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;

namespace TestCase
{
    public sealed record ChildSource(int Value, bool Fail = false);

    public sealed record ChildDestination(int Value);

    public sealed record OuterSource(ChildSource Child);

    public sealed record OuterDestination(ChildDestination Child);

    public sealed record Frame(
        string Name,
        MappingOperation Operation,
        IMapper Mapper);

    public sealed class TestException : Exception
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static List<Frame> Frames { get; } = new();

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<OuterSource, OuterDestination>()
                .Convert((source, previous, context) =>
                {
                    Frames.Add(new(
                        "outer-before",
                        context.Operation,
                        context.Mapper));

                    ChildDestination child;

                    try
                    {
                        child = previous.TryGetValue(out var destination)
                            ? context.Mapper.Map(
                                source!.Child,
                                destination.Child)
                            : context.Mapper.Map<
                                ChildSource,
                                ChildDestination>(source!.Child);
                    }
                    catch (TestException)
                    {
                        child = context.Mapper.Map<
                            ChildSource,
                            ChildDestination>(new ChildSource(7));
                    }

                    Frames.Add(new(
                        "outer-after",
                        context.Operation,
                        context.Mapper));

                    return new OuterDestination(child);
                });

            builder.Map<ChildSource, ChildDestination>()
                .Convert((source, previous, context) =>
                {
                    Frames.Add(new(
                        "child",
                        context.Operation,
                        context.Mapper));

                    if (source!.Fail)
                    {
                        throw new TestException();
                    }

                    return previous.TryGetValue(out var destination)
                        ? new ChildDestination(
                            destination.Value + source.Value)
                        : new ChildDestination(source.Value);
                });
        }
    }

    public sealed class ManualServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        public object? GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var service)
                ? service
                : null;

        public void Add<TService>(TService service)
            where TService : class =>
            _services[typeof(IEnumerable<TService>)] =
                new TService[] { service };
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var generated = new TestMapper();
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<OuterSource, OuterDestination>>(
                generated);
            provider.Add<ITypeMapper<ChildSource, ChildDestination>>(
                generated);
            var mapper = new Mapper(provider);
            var created = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(new ChildSource(2)));
            var previous = new OuterDestination(
                new ChildDestination(10));
            var updated = mapper.Map(
                new OuterSource(new ChildSource(3)),
                previous);
            var recovered = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(new ChildSource(0, Fail: true)));

            if (created.Child.Value != 2 ||
                updated.Child.Value != 13 ||
                ReferenceEquals(previous, updated) ||
                recovered.Child.Value != 7)
            {
                throw new InvalidOperationException(
                    "Nested Convert returned the wrong result.");
            }

            var frames = TestMapper.Frames;

            if (frames.Count != 10 ||
                frames[0].Name != "outer-before" ||
                frames[0].Operation != MappingOperation.Create ||
                frames[1].Name != "child" ||
                frames[1].Operation != MappingOperation.Create ||
                frames[2].Name != "outer-after" ||
                frames[2].Operation != MappingOperation.Create ||
                frames[3].Name != "outer-before" ||
                frames[3].Operation != MappingOperation.Update ||
                frames[4].Name != "child" ||
                frames[4].Operation != MappingOperation.Update ||
                frames[5].Name != "outer-after" ||
                frames[5].Operation != MappingOperation.Update ||
                frames[6].Name != "outer-before" ||
                frames[6].Operation != MappingOperation.Create ||
                frames[7].Name != "child" ||
                frames[7].Operation != MappingOperation.Create ||
                frames[8].Name != "child" ||
                frames[8].Operation != MappingOperation.Create ||
                frames[9].Name != "outer-after" ||
                frames[9].Operation != MappingOperation.Create)
            {
                throw new InvalidOperationException(
                    "Nested Convert observed the wrong call frames.");
            }

            AssertSameMapper(frames, 0, 2);
            AssertSameMapper(frames, 3, 5);
            AssertSameMapper(frames, 6, 9);

            if (ReferenceEquals(frames[0].Mapper, frames[3].Mapper) ||
                ReferenceEquals(frames[3].Mapper, frames[6].Mapper) ||
                ReferenceEquals(frames[0].Mapper, frames[6].Mapper))
            {
                throw new InvalidOperationException(
                    "Independent roots reused a scoped mapper.");
            }
        }

        private static void AssertSameMapper(
            IReadOnlyList<Frame> frames,
            int first,
            int last)
        {
            for (var index = first + 1; index <= last; index++)
            {
                if (!ReferenceEquals(
                        frames[first].Mapper,
                        frames[index].Mapper))
                {
                    throw new InvalidOperationException(
                        "One mapping chain changed its scoped mapper.");
                }
            }
        }
    }
}
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
