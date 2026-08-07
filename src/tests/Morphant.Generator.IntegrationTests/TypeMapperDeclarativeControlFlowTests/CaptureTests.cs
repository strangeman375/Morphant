using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperDeclarativeControlFlowTests;

[TestFixture]
internal sealed class CaptureTests
{
    [Test]
    public void Transfers_constants_and_mapper_members_but_rejects_runtime_Configure_local()
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

    public sealed class GoodDestination
    {
        public int Value { get; set; }
    }

    public sealed class BadDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        private readonly int result = 1;

        private readonly int value = 2;

        private int Offset => result + value;

        protected override void Configure(MapperBuilder builder)
        {
            const int factor = 3;
            var runtime = 5;

            builder.Map<Source, GoodDestination>()
                .Members((source, _) =>
                {
                    var value = source.Value * factor;
                    return new()
                    {
                        Value = Scale(value) + Offset
                    };
                });

            builder.Map<Source, BadDestination>()
                .Members((source, _) => new()
                {
                    Value = source.Value + runtime
                });
        }

        private static int Scale(int input) => input * 2;
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var good = ((ITypeMapper<Source, GoodDestination>)mapper)
                .Create(new Source { Value = 4 }, context);

            if (good.Value != 27)
            {
                throw new InvalidOperationException(
                    "Supported declarative captures were not transferred.");
            }

            try
            {
                ((ITypeMapper<Source, BadDestination>)mapper)
                    .Create(new Source { Value = 4 }, context);
            }
            catch (NotSupportedException exception)
                when (exception.Message.Contains(
                    "capture",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new InvalidOperationException(
                "A runtime Configure local was transferred.");
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
