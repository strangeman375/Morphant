using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class SharedValuesTests
{
    [Test]
    public void Shares_constructor_member_and_nested_subexpressions()
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
        public Destination(long seed) => Seed = seed;

        public long Seed { get; }

        public int First { get; set; }

        public long Second { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
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
            var created = mapper.Map(
                new Source { Value = 3 },
                context);

            if (created.Seed != 103 ||
                created.First != 103 ||
                created.Second != 103 ||
                created.Text != "103" ||
                TestMapper.InvocationCount != 1)
            {
                throw new InvalidOperationException(
                    $"Create did not share the common value: " +
                    $"seed={created.Seed}, first={created.First}, " +
                    $"second={created.Second}, text={created.Text}, " +
                    $"count={TestMapper.InvocationCount}.");
            }

            var previous = new Destination(7);
            var updated = mapper.Map(
                new Source { Value = 4 },
                previous,
                context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Seed != 7 ||
                updated.First != 204 ||
                updated.Second != 204 ||
                updated.Text != "204" ||
                TestMapper.InvocationCount != 2)
            {
                throw new InvalidOperationException(
                    $"Update did not share duplicate member values: " +
                    $"seed={updated.Seed}, first={updated.First}, " +
                    $"second={updated.Second}, text={updated.Text}, " +
                    $"count={TestMapper.InvocationCount}.");
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
