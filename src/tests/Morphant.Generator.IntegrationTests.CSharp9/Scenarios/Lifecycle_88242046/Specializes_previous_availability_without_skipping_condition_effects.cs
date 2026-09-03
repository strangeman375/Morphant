// Compiled integration scenario: TypeMapperStructuredConstructTests/LifecycleTests::Specializes_previous_availability_without_skipping_condition_effects
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Lifecycle_88242046
{
    public sealed class Source
    {
        public int Id { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static int BeforePreviousCount { get; private set; }

        public static int AfterPreviousCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Resolve((source, previous) =>
                {
                    if (TrackBefore() &&
                        previous.HasValue &&
                        TrackAfter())
                    {
                        return previous;
                    }

                    return new(source.Id);
                });

        private static bool TrackBefore()
        {
            BeforePreviousCount++;
            return true;
        }

        private static bool TrackAfter()
        {
            AfterPreviousCount++;
            return true;
        }
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
            var createdByUpdate = mapper.Update(source, null, context);
            var previous = new Destination(31);
            var updated = mapper.Update(source, previous, context);

            if (created.Id != 17 ||
                createdByUpdate.Id != 17 ||
                !ReferenceEquals(previous, updated) ||
                TestMapper.BeforePreviousCount != 3 ||
                TestMapper.AfterPreviousCount != 1)
            {
                throw new InvalidOperationException(
                    "Previous specialization changed condition evaluation.");
            }
        }
    }
}
