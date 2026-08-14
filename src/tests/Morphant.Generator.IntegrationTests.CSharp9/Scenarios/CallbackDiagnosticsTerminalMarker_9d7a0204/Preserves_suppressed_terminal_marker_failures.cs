// Compiled integration scenario: CallbackDiagnosticsTests::Suppressed_grammar_mutation_and_marker_failures_do_not_escape
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0033

using System;
using Morphant;
using Morphant.Context;
using Morphant.Markers;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CallbackDiagnosticsTerminalMarker_9d7a0204
{
    public sealed class ChildSource
    {
        public int Value { get; init; }
    }

    public sealed class ChildDestination
    {
        public ChildDestination(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class Source
    {
        public ChildSource Child { get; init; } = new ChildSource();
    }

    public sealed class InvalidConstructDestination
    {
        public InvalidConstructDestination(ChildDestination child) =>
            Child = child;

        public ChildDestination Child { get; }
    }

    public sealed class InvalidResolveDestination
    {
        public InvalidResolveDestination(ChildDestination child) =>
            Child = child;

        public ChildDestination Child { get; }
    }

    public sealed class InvalidMembersDestination
    {
        public ChildDestination Child { get; set; } =
            new ChildDestination(-1);
    }

    public sealed class ValidConstructDestination
    {
        public ValidConstructDestination(ChildDestination child) =>
            Child = child;

        public ChildDestination Child { get; }
    }

    public sealed class ValidResolveDestination
    {
        public ValidResolveDestination(ChildDestination child) =>
            Child = child;

        public ChildDestination Child { get; }
    }

    public sealed class ValidMembersDestination
    {
        public ChildDestination Child { get; set; } =
            new ChildDestination(-1);
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>()
                .Construct(source => new(source.Value));

            builder.Map<Source, InvalidConstructDestination>()
                .Construct(source => new(
                    Consume(Map<ChildDestination>(source.Child))));

            builder.Map<Source, InvalidResolveDestination>()
                .Resolve((source, _) => new(
                    Consume(Map<ChildDestination>(source.Child))));

            builder.Map<Source, InvalidMembersDestination>()
                .Members(source => new()
                {
                    Child = Consume(
                        Map<ChildDestination>(source.Child))
                });

            builder.Map<Source, ValidConstructDestination>()
                .Construct(source => new(
                    Map<ChildDestination>(source.Child)));

            builder.Map<Source, ValidResolveDestination>()
                .Resolve((source, _) => new(
                    Map<ChildDestination>(source.Child)));

            builder.Map<Source, ValidMembersDestination>()
                .Members(source => new()
                {
                    Child = Map<ChildDestination>(source.Child)
                });
        }

        private static ChildDestination Consume(
            MapMarker<ChildDestination> marker) =>
            new ChildDestination(-100);

        private static ChildDestination Consume(
            ChildDestination value) =>
            new ChildDestination(value.Value + 1000);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source
            {
                Child = new ChildSource { Value = 7 }
            };

            ExpectUnsupported<InvalidConstructDestination>(mapper, source);
            ExpectUnsupported<InvalidResolveDestination>(mapper, source);
            ExpectUnsupported<InvalidMembersDestination>(mapper, source);

            AssertValue<ValidConstructDestination>(
                mapper,
                source,
                destination => destination.Child.Value);
            AssertValue<ValidResolveDestination>(
                mapper,
                source,
                destination => destination.Child.Value);
            AssertValue<ValidMembersDestination>(
                mapper,
                source,
                destination => destination.Child.Value);
        }

        private static void ExpectUnsupported<TDestination>(
            TestMapper mapper,
            Source source)
        {
            try
            {
                ((ITypeMapper<Source, TDestination>)mapper).Create(
                    source,
                    default(MappingContext));
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A non-terminal nested marker was executed.");
        }

        private static void AssertValue<TDestination>(
            TestMapper mapper,
            Source source,
            Func<TDestination, int> read)
        {
            var destination =
                ((ITypeMapper<Source, TDestination>)mapper).Create(source);

            if (read(destination) != 7)
            {
                throw new InvalidOperationException(
                    "A terminal nested marker was not lowered.");
            }
        }
    }
}
