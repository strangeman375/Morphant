using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class OverlayTests
{
    [Test]
    public void Removes_overridden_dependencies_before_sharing()
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
    }

    public sealed class Destination
    {
        public Destination(int seed) => Seed = seed;

        public int Seed { get; }

        public int Value { get; set; }

        public int Other { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int SharedCount { get; private set; }

        public static int DiscardedCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(Shared(source.Value)))
                .Members((source, _) =>
                {
                    var baseline = new DestinationMembers
                    {
                        Value = Discarded(source.Value),
                        Other = Shared(source.Value)
                    };

                    return baseline with
                    {
                        Value = Shared(source.Value)
                    };
                });

        private static int Shared(int value)
        {
            SharedCount++;
            return value + SharedCount * 10;
        }

        private static int Discarded(int value)
        {
            DiscardedCount++;
            return value;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var result = mapper.Map(
                new Source { Value = 2 },
                default(MappingContext));

            if (result.Seed != 12 ||
                result.Value != 12 ||
                result.Other != 12 ||
                TestMapper.SharedCount != 1 ||
                TestMapper.DiscardedCount != 0)
            {
                throw new InvalidOperationException(
                    "An overridden dependency survived the effective plan.");
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
