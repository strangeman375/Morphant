// Compiled integration scenario: TypeMapperConventionTests/ConstructorTests::Selects_the_only_parameterized_constructor_and_maps_its_arguments
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Constructor_4542a26b
{
    public sealed class Source
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public Destination()
        {
            Id = -1;
            Name = "parameterless";
        }

        internal Destination(int id, string name = "optional")
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }

        public string Name { get; }
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
                    Id = 23,
                    Name = "parameterized"
                },
                default(MappingContext));

            if (result.Id != 23 || result.Name != "parameterized")
            {
                throw new InvalidOperationException(
                    "The unambiguous constructor was not used.");
            }
        }
    }
}
