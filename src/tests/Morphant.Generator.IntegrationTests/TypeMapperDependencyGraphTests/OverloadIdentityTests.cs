using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class OverloadIdentityTests
{
    [Test]
    public void Does_not_share_same_text_bound_to_different_overloads()
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

        public long Value { get; set; }
    }

    public sealed class Resolver
    {
        public int IntCount { get; private set; }

        public int LongCount { get; private set; }

        public int Resolve(int value)
        {
            IntCount++;
            return value + 10;
        }

        public long Resolve(long value)
        {
            LongCount++;
            return value + 100;
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static Resolver Service { get; } = new();

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source =>
                {
                    int value = source.Value;
                    return new(Service.Resolve(value));
                })
                .Members((source, _) =>
                {
                    long value = source.Value;
                    return new()
                    {
                        Value = Service.Resolve(value)
                    };
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var result = mapper.Create(
                new Source { Value = 1 },
                default(MappingContext));

            if (result.Seed != 11 ||
                result.Value != 101 ||
                TestMapper.Service.IntCount != 1 ||
                TestMapper.Service.LongCount != 1)
            {
                throw new InvalidOperationException(
                    "Different overloads were merged.");
            }
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
