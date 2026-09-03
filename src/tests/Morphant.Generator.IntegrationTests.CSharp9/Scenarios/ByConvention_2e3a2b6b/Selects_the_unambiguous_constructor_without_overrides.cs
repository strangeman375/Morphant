// Compiled integration scenario: TypeMapperStructuredConstructTests/ByConventionTests::Selects_the_unambiguous_constructor_without_overrides
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ByConvention_2e3a2b6b
{
    public sealed class Source
    {
        public int Id { get; init; }
    }

    public sealed class Destination
    {
        public Destination()
        {
            Kind = "parameterless";
        }

        public Destination(int id)
        {
            Kind = "parameterized";
            Id = id;
        }

        public string Kind { get; }

        public int Id { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(_ => new(ByConvention()));
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source { Id = 17 };
            var context = default(MappingContext);
            var created = mapper.Create(source, context);
            var previous = new Destination(31);
            var updated = mapper.Update(source, previous, context);

            if (created.Kind != "parameterized" ||
                created.Id != 17 ||
                !ReferenceEquals(previous, updated) ||
                updated.Id != 31)
            {
                throw new InvalidOperationException(
                    "ByConvention did not use unambiguous construction semantics.");
            }
        }
    }
}
