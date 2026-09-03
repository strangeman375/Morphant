// Compiled integration scenario: TypeMapperStandaloneDispatchTests::Uses_generated_exact_pairs_from_the_mapper_hierarchy

#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Exceptions;

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

    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : BaseMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<ChildSource, ChildDestination>();
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper<DerivedMapper>
    {
        public static IMapper? CapturedMapper { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<OuterSource, OuterDestination>()
                .Convert((source, _, context) =>
                {
                    CapturedMapper = context.Mapper;
                    return new OuterDestination(
                        context.Mapper.Map<
                            ChildSource,
                            ChildDestination>(source!.Child));
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new DerivedMapper();
            var contract =
                (ITypeMapper<OuterSource, OuterDestination>)mapper;
            var created = contract.Create(
                new OuterSource
                {
                    Child = new ChildSource { Value = 17 }
                });
            var supplied = new OuterDestination(
                new ChildDestination { Value = -1 });
            var updated = contract.Update(
                new OuterSource
                {
                    Child = new ChildSource { Value = 18 }
                },
                supplied);

            if (created.Child.Value != 17 ||
                ReferenceEquals(updated, supplied) ||
                updated.Child.Value != 18)
            {
                throw new InvalidOperationException(
                    "The generated standalone dispatch did not include an " +
                    "exact pair inherited from the mapper hierarchy.");
            }

            try
            {
                DerivedMapper.CapturedMapper!.Map<
                    ChildSource,
                    ChildDestination>(new ChildSource());
            }
            catch (MappingScopeCompletedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "The standalone mapping scope remained active.");
        }
    }
}
