// Compiled integration scenario: TypeMapperEvaluationTests/ConversionTests::Evaluates_an_implicit_conversion_once_when_its_result_is_reused
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Conversion_bd5d4907
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public readonly struct Wrapped
    {
        public Wrapped(int value) => Value = value;

        public static int ConversionCount { get; private set; }

        public int Value { get; }

        public static implicit operator Wrapped(int value)
        {
            ConversionCount++;
            return new Wrapped(value + ConversionCount * 100);
        }
    }

    public sealed class Destination
    {
        public Destination(Wrapped seed) => Seed = seed;

        public Wrapped Seed { get; }

        public Wrapped First { get; set; }

        public Wrapped Second { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int InvocationCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(
                    (Wrapped)Next(source.Value)))
                .Members((source, _) => new()
                {
                    First = (Wrapped)Next(source.Value),
                    Second = (Wrapped)Next(source.Value)
                });

        private static int Next(int value)
        {
            InvocationCount++;
            return value + 10;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var context = default(MappingContext);
            var created = mapper.Create(
                new Source { Value = 3 },
                context);

            if (created.Seed.Value != 113 ||
                created.First.Value != 113 ||
                created.Second.Value != 113 ||
                TestMapper.InvocationCount != 1 ||
                Wrapped.ConversionCount != 1)
            {
                throw new InvalidOperationException(
                    "Create did not share the target conversion.");
            }

            var previous = new Destination(new Wrapped(1));
            var updated = mapper.Update(
                new Source { Value = 4 },
                previous,
                context);

            if (!ReferenceEquals(previous, updated) ||
                updated.First.Value != 214 ||
                updated.Second.Value != 214 ||
                TestMapper.InvocationCount != 2 ||
                Wrapped.ConversionCount != 2)
            {
                throw new InvalidOperationException(
                    "Update did not share the target conversion.");
            }
        }
    }
}
