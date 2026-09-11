// Compiled integration scenario: TypeMapperEvaluationTests/ResultValuesTests::Evaluates_each_result_dependent_call_after_construction
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ResultValues_9bf7563d
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int seed) => Seed = seed;

        public int Seed { get; }

        public int First { get; set; }

        public int Second { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static int InvocationCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(source.Value))
                .Members((_, _, result) => new()
                {
                    First = Next(result.Seed),
                    Second = Next(result.Seed)
                });

        private static int Next(int value)
        {
            InvocationCount++;
            return value + InvocationCount * 100;
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
                new Source { Value = 5 },
                context);

            if (created.Seed != 5 ||
                created.First != 105 ||
                created.Second != 205 ||
                TestMapper.InvocationCount != 2)
            {
                throw new InvalidOperationException(
                    "Create must evaluate each result-dependent call.");
            }

            var previous = new Destination(7);
            var updated = mapper.Update(
                new Source { Value = 9 },
                previous,
                context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Seed != 7 ||
                updated.First != 307 ||
                updated.Second != 407 ||
                TestMapper.InvocationCount != 4)
            {
                throw new InvalidOperationException(
                    "Update must evaluate each result-dependent call.");
            }
        }
    }
}
