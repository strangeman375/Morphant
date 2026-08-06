using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class ResultValuesTests
{
    [Test]
    public void Shares_values_only_after_the_result_exists()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace TestCase
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
    public partial class TestMapper : TypeMapper
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
                created.Second != 105 ||
                TestMapper.InvocationCount != 1)
            {
                throw new InvalidOperationException(
                    "Create did not share the result-dependent value.");
            }

            var previous = new Destination(7);
            var updated = mapper.Update(
                new Source { Value = 9 },
                previous,
                context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Seed != 7 ||
                updated.First != 207 ||
                updated.Second != 207 ||
                TestMapper.InvocationCount != 2)
            {
                throw new InvalidOperationException(
                    "Update did not share the result-dependent value.");
            }
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
