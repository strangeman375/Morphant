// Compiled integration scenario: TypeMapperStandaloneDispatchTests::Uses_generated_exact_pairs_from_the_mapper_hierarchy

#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.StandaloneDispatch_8c2f1a4b
{
    public sealed class ChildSource
    {
        public int Value { get; init; }
    }

    public sealed class ChildDestination
    {
        public int Value { get; set; }
    }

    public sealed class OuterSource
    {
        public ChildSource Child { get; init; } = new ChildSource();
    }

    public sealed class OuterDestination
    {
        public OuterDestination(ChildDestination child)
        {
            Child = child;
        }

        public ChildDestination Child { get; }
    }

    [MorphantMapper]
    public partial class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<ChildSource, ChildDestination>();
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<OuterSource, OuterDestination>()
                .Convert((source, _, context) =>
                    new OuterDestination(
                        context.Mapper.Map<
                            ChildSource,
                            ChildDestination>(source!.Child)));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new DerivedMapper();
            var result = mapper.Create<OuterSource, OuterDestination>(
                new OuterSource
                {
                    Child = new ChildSource { Value = 17 }
                });

            if (result.Child.Value != 17)
            {
                throw new InvalidOperationException(
                    "The generated standalone dispatch did not include an " +
                    "exact pair inherited from the mapper hierarchy.");
            }
        }
    }
}
