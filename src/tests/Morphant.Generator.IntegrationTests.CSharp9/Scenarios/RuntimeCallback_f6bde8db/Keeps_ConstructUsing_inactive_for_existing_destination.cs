// Compiled integration scenario: TypeMapperRuntimeConstructionTests/RuntimeCallbackLifecycleTests::Keeps_ConstructUsing_inactive_for_existing_destination
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimeCallback_f6bde8db
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public interface IDestination
    {
        int Value { get; set; }
    }

    public sealed class Destination : IDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int ConstructionCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, IDestination>()
                .ConstructUsing(source => Create(source.Value));

        private static IDestination Create(int value)
        {
            ConstructionCount++;
            return new Destination { Value = value + 10 };
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, IDestination>)new TestMapper();
            var context = default(MappingContext);
            var source = new Source { Value = 7 };
            var created = mapper.Create(source, context);
            var previous = new Destination();
            var updated = mapper.Update(source, previous, context);

            if (created.Value != 7 ||
                !ReferenceEquals(previous, updated) ||
                updated.Value != 7 ||
                TestMapper.ConstructionCount != 1)
            {
                throw new InvalidOperationException(
                    "ConstructUsing ran for an existing destination.");
            }
        }
    }
}
