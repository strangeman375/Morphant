// Compiled integration scenario: TypeMapperDependencyGraphTests/WrapperTests::Ignores_transparent_nullability_and_parenthesis_wrappers
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Wrapper_7b8f2fe2
{
    public sealed class Source
    {
        public string? Value { get; init; }
    }

    public sealed class Destination
    {
        public Destination(string seed) => Seed = seed;

        public string Seed { get; }

        public string First { get; set; } = string.Empty;

        public string Second { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int InvocationCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source =>
                {
                    var normalized = Normalize(source.Value)!;
                    return new(normalized);
                })
                .Members((source, _) =>
                {
                    var normalized = (Normalize(source.Value))!;
                    return new()
                    {
                        First = normalized,
                        Second = normalized
                    };
                });

        private static string? Normalize(string? value)
        {
            InvocationCount++;
            return value ?? string.Empty;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var result = mapper.Create(
                new Source { Value = "value" },
                default(MappingContext));

            if (result.Seed != "value" ||
                result.First != "value" ||
                result.Second != "value" ||
                TestMapper.InvocationCount != 1)
            {
                throw new InvalidOperationException(
                    "Transparent wrappers split one dependency node.");
            }
        }
    }
}
