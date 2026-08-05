using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class SemanticIdentityTests
{
    [Test]
    public void Does_not_share_same_text_bound_to_different_symbols()
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

        public int Value { get; set; }
    }

    public sealed class FirstResolver
    {
        public int Count { get; private set; }

        public int Resolve(int value)
        {
            Count++;
            return value + 10;
        }
    }

    public sealed class SecondResolver
    {
        public int Count { get; private set; }

        public int Resolve(int value)
        {
            Count++;
            return value + 100;
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static FirstResolver First { get; } = new();

        public static SecondResolver Second { get; } = new();

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .Construct(source =>
                {
                    var service = First;
                    var value = service.Resolve(source.Value);
                    return new(value);
                })
                .Members((source, _) =>
                {
                    var service = Second;
                    var value = service.Resolve(source.Value);
                    return new()
                    {
                        Value = value
                    };
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var result = mapper.Map(
                new Source { Value = 1 },
                default(MappingContext));

            if (result.Seed != 11 ||
                result.Value != 101 ||
                TestMapper.First.Count != 1 ||
                TestMapper.Second.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Textual equality incorrectly merged bound symbols: " +
                    $"result=({result.Seed},{result.Value}), " +
                    $"counts=({TestMapper.First.Count}," +
                    $"{TestMapper.Second.Count}).");
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
