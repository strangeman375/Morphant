// Compiled integration scenario: TypeMapperStructuredConstructTests/ExplicitConstructorTests::Executes_source_only_constructor_for_Create_and_null_Update
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExplicitConstructor_7f173aff
{
    public sealed class Source
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public Destination(int id)
        {
            Id = id;
        }

        public int Id { get; }

        public string Name { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int ConstructionCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(Track(source.Id)));

        private static int Track(int id)
        {
            ConstructionCount++;
            return id;
        }
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
            var createdByUpdate = mapper.Update(source, null, context);
            var previous = new Destination(31);
            var updated = mapper.Update(source, previous, context);

            if (created.Id != 17 ||
                created.Name != "mapped" ||
                createdByUpdate.Id != 17 ||
                createdByUpdate.Name != "mapped" ||
                !ReferenceEquals(previous, updated) ||
                updated.Id != 31 ||
                updated.Name != "mapped" ||
                TestMapper.ConstructionCount != 2)
            {
                throw new InvalidOperationException(
                    "Source-only structured Construct was not executed correctly.");
            }
        }
    }

}
