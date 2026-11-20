// Compiled integration scenario: TypeMapperConventionTests/ConstructorTests::Omits_an_unmatched_optional_constructor_parameter
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Constructor_12b1639c
{
    public sealed class Source
    {
        public int Id { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int id, string label = "fallback")
        {
            Id = id;
            Label = label;
        }

        public int Id { get; }

        public string Label { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var result = mapper.Create(
                new Source
                {
                    Id = 83
                },
                default(MappingContext));

            if (result.Id != 83 || result.Label != "fallback")
            {
                throw new InvalidOperationException(
                    "An optional constructor parameter was not omitted.");
            }
        }
    }
}
