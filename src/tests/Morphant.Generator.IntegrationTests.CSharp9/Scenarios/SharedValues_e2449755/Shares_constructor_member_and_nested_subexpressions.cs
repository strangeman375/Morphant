// Compiled integration scenario: TypeMapperEvaluationTests/SharedValuesTests::Evaluates_each_repeated_expression_across_constructor_and_members
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SharedValues_e2449755
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public Destination(long seed) => Seed = seed;

        public long Seed { get; }

        public int First { get; set; }

        public long Second { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static int InvocationCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source =>
                {
                    var shared = Next(source.Value);
                    return new(shared);
                })
                .Members((source, _) => new()
                {
                    First = ((Next(source.Value))),
                    Second = Next(source.Value),
                    Text = Next(source.Value).ToString()
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
                new Source { Value = 3 },
                context);

            if (created.Seed != 103 ||
                created.First != 203 ||
                created.Second != 303 ||
                created.Text != "403" ||
                TestMapper.InvocationCount != 4)
            {
                throw new InvalidOperationException(
                    $"Create lost a separate evaluation: " +
                    $"seed={created.Seed}, first={created.First}, " +
                    $"second={created.Second}, text={created.Text}, " +
                    $"count={TestMapper.InvocationCount}.");
            }

            var previous = new Destination(7);
            var updated = mapper.Update(
                new Source { Value = 4 },
                previous,
                context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Seed != 7 ||
                updated.First != 504 ||
                updated.Second != 604 ||
                updated.Text != "704" ||
                TestMapper.InvocationCount != 7)
            {
                throw new InvalidOperationException(
                    $"Update lost a separate member evaluation: " +
                    $"seed={updated.Seed}, first={updated.First}, " +
                    $"second={updated.Second}, text={updated.Text}, " +
                    $"count={TestMapper.InvocationCount}.");
            }
        }
    }
}
