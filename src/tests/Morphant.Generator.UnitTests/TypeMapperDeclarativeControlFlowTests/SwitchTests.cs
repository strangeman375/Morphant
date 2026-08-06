using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperDeclarativeControlFlowTests;

[TestFixture]
internal sealed class SwitchTests
{
    [Test]
    public void Executes_complete_statement_switch_and_pattern_variables()
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
        public object? Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }

        public string Path { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int GuardCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, previous) =>
                {
                    switch (source.Value)
                    {
                        case int number when Accept(number):
                            return new()
                            {
                                Value = number,
                                Path = previous.HasValue
                                    ? "integer-update"
                                    : "integer-create"
                            };

                        case string { Length: > 0 } text:
                            return new()
                            {
                                Value = text.Length,
                                Path = "text"
                            };

                        default:
                            return new()
                            {
                                Value = -1,
                                Path = "fallback"
                            };
                    }
                });

        private static bool Accept(int value)
        {
            GuardCount++;
            return value > 0;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var context = default(MappingContext);
            var number = mapper.Create(
                new Source { Value = 7 },
                context);
            var previous = new Destination();
            var text = mapper.Update(
                new Source { Value = "abcd" },
                previous,
                context);
            var fallback = mapper.Create(
                new Source { Value = 0 },
                context);

            if (number.Value != 7 ||
                number.Path != "integer-create" ||
                !ReferenceEquals(previous, text) ||
                text.Value != 4 ||
                text.Path != "text" ||
                fallback.Value != -1 ||
                fallback.Path != "fallback" ||
                TestMapper.GuardCount != 2)
            {
                throw new InvalidOperationException(
                    "The statement switch was lowered incorrectly.");
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
