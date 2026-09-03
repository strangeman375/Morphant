// Compiled integration scenario: TypeMapperStructuredConstructTests/LifecycleTests::Selects_previous_or_replacement_without_evaluating_other_branches
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Lifecycle_ee1a3190
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
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static int ConstructionCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Resolve((source, previous) =>
                {
                    if (previous.HasValue &&
                        previous.Value.Id == source.Id)
                    {
                        return previous;
                    }

                    return new(Track(source.Id));
                });

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
            var context = default(MappingContext);
            var source = new Source
            {
                Id = 17,
                Name = "mapped"
            };
            var created = mapper.Create(source, context);
            var createdByUpdate = mapper.Update(source, null, context);
            var reusable = new Destination(17);
            var reused = mapper.Update(source, reusable, context);
            var replaced = mapper.Update(
                source,
                new Destination(31),
                context);

            if (created.Id != 17 ||
                created.Name != "mapped" ||
                createdByUpdate.Id != 17 ||
                createdByUpdate.Name != "mapped" ||
                !ReferenceEquals(reusable, reused) ||
                reused.Name != "mapped" ||
                replaced.Id != 17 ||
                replaced.Name != "mapped" ||
                TestMapper.ConstructionCount != 3)
            {
                throw new InvalidOperationException(
                    "Previous-aware Construct selected or evaluated the wrong branch.");
            }
        }
    }
}
