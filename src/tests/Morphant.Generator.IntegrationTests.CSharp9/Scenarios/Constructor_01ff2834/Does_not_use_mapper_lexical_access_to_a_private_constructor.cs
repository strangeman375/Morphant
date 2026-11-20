// Compiled integration scenario: TypeMapperConventionTests/ConstructorTests::Does_not_use_mapper_lexical_access_to_a_private_constructor
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0035

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Constructor_01ff2834
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public sealed class Destination
        {
            private Destination()
            {
            }

            public int Value { get; set; }

            public static Destination Existing() => new();
        }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, TestMapper.Destination>)
                new TestMapper();
            var previous = TestMapper.Destination.Existing();
            var updated = mapper.Update(
                new Source
                {
                    Value = 67
                },
                previous,
                default(MappingContext));

            if (!ReferenceEquals(updated, previous) || updated.Value != 67)
            {
                throw new InvalidOperationException(
                    "Update through an assembly-stable surface failed.");
            }

            try
            {
                _ = mapper.Create(
                    new Source(),
                    default(MappingContext));
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Mapper lexical access leaked into constructor selection.");
        }
    }
}
