using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class AliasingTests
{
    [Test]
    public void Shares_an_alias_read_without_contracting_assignment_order()
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
    public sealed class Mutable
    {
        public Mutable(int value) => Value = value;

        public int Value { get; set; }

        public int Copy { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int ReadCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Mutable, Mutable>()
                .Construct(source => new(Observe(source.Value)))
                .Members((source, _) => new()
                {
                    Value = Observe(source.Value),
                    Copy = Observe(source.Value)
                });

        private static int Observe(int value)
        {
            ReadCount++;
            return value + 10;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Mutable, Mutable>)
                new TestMapper();
            var value = new Mutable(5);
            var result = mapper.Map(
                value,
                value,
                default(MappingContext));

            if (!ReferenceEquals(value, result) ||
                result.Value != 15 ||
                result.Copy != 15 ||
                TestMapper.ReadCount != 1)
            {
                throw new InvalidOperationException(
                    "The aliased source value was not shared once.");
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
