// Compiled integration scenario: TypeMapperMappingModeTests::Gates_Create_and_Update_without_changing_the_shared_contract
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MappingMode_a9e37137
{
    public readonly struct Source
    {
        public int Value { get; init; }
    }

    public sealed class CreateDestination
    {
        public int Value { get; set; }
    }

    public sealed class UpdateDestination
    {
        public int Value { get; set; }
    }

    public sealed class BothDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, CreateDestination>(MappingMode.Create);
            builder.Map<Source, UpdateDestination>(MappingMode.Update);
            builder.Map<Source, BothDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 17 };
            var context = default(MappingContext);
            var create =
                (ITypeMapper<Source, CreateDestination>)mapper;
            var update =
                (ITypeMapper<Source, UpdateDestination>)mapper;
            var both =
                (ITypeMapper<Source, BothDestination>)mapper;

            if (create.Create(source, context).Value != 17)
            {
                throw new InvalidOperationException(
                    "Create did not map a new destination.");
            }

            ExpectNotSupported(() =>
                create.Update(source, new CreateDestination(), context));
            ExpectNotSupported(() => update.Create(source, context));

            var previous = new UpdateDestination();
            var updated = update.Update(source, previous, context);
            var createdByUpdate = update.Update(source, null, context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Value != 17 ||
                createdByUpdate.Value != 17)
            {
                throw new InvalidOperationException(
                    "Update did not preserve its declarative semantics.");
            }

            var bothPrevious = new BothDestination();
            var bothCreated = both.Create(source, context);
            var bothUpdated = both.Update(source, bothPrevious, context);

            if (bothCreated.Value != 17 ||
                !ReferenceEquals(bothPrevious, bothUpdated) ||
                bothUpdated.Value != 17)
            {
                throw new InvalidOperationException(
                    "CreateAndUpdate did not enable both operations.");
            }
        }

        private static void ExpectNotSupported(Action action)
        {
            try
            {
                action();
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A disabled operation did not throw NotSupportedException.");
        }
    }
}
