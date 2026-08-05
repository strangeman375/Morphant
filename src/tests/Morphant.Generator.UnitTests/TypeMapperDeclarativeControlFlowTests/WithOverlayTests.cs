using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperDeclarativeControlFlowTests;

[TestFixture]
internal sealed class WithOverlayTests
{
    [Test]
    public void Applies_record_with_overlay_and_removes_overridden_rule()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using TestCase.Morphant.Generated;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }

        public int Automatic { get; init; }

        public bool Alternate { get; init; }

        public bool IgnoreValue { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }

        public int Automatic { get; set; }

        public string Path { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int OverriddenCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) =>
                {
                    var baseline = source.Alternate
                        ? new DestinationMembers
                        {
                            Value = Overridden(source.Value),
                            Path = "alternate"
                        }
                        : new DestinationMembers
                        {
                            Value = Overridden(source.Value),
                            Path = "normal"
                        };

                    return baseline with
                    {
                        Value = source.IgnoreValue
                            ? Ignore<int>()
                            : source.Value * 10,
                        Automatic = Auto()
                    };
                });

        private static int Overridden(int value)
        {
            OverriddenCount++;
            return value;
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
                new Source
                {
                    Value = 3,
                    Automatic = 7,
                    Alternate = true
                },
                context);
            var previous = new Destination { Value = 19 };
            var updated = mapper.Map(
                new Source
                {
                    Value = 4,
                    Automatic = 8,
                    IgnoreValue = true
                },
                previous,
                context);

            if (created.Value != 30 ||
                created.Automatic != 7 ||
                created.Path != "alternate" ||
                !ReferenceEquals(previous, updated) ||
                updated.Value != 19 ||
                updated.Automatic != 8 ||
                updated.Path != "normal" ||
                TestMapper.OverriddenCount != 0)
            {
                throw new InvalidOperationException(
                    "The record with overlay was lowered incorrectly.");
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
