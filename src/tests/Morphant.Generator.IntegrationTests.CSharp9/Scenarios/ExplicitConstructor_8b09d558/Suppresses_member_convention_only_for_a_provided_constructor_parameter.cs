// Compiled integration scenario: TypeMapperStructuredConstructTests/ExplicitConstructorTests::Suppresses_member_convention_only_for_a_provided_constructor_parameter
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExplicitConstructor_8b09d558
{
    public sealed class Source
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public Destination(
            int id,
            string name = "constructor-default")
        {
            Id = id + 1;
            Name = name;
        }

        public int Id { get; set; }

        public string Name { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(source.Id));
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source
            {
                Id = 17,
                Name = "mapped"
            };
            var context = default(MappingContext);
            var created = mapper.Create(source, context);
            var previous = new Destination(0);
            var updated = mapper.Update(source, previous, context);

            if (created.Id != 18 ||
                created.Name != "mapped" ||
                !ReferenceEquals(previous, updated) ||
                updated.Id != 17 ||
                updated.Name != "mapped")
            {
                throw new InvalidOperationException(
                    "Constructor arguments occupied the wrong convention members.");
            }
        }
    }
}
