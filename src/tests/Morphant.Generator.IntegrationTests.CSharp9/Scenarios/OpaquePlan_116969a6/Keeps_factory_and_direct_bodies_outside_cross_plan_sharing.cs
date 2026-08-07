// Compiled integration scenario: TypeMapperDependencyGraphTests/OpaquePlanTests::Keeps_factory_and_direct_bodies_outside_cross_plan_sharing
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.OpaquePlan_116969a6
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int seed) => Seed = seed;

        public int Seed { get; }

        public int Value { get; set; }
    }

    public interface IDirectDestination
    {
        int Seed { get; }

        int Value { get; set; }
    }

    public sealed class DirectDestination : IDirectDestination
    {
        public DirectDestination(int seed) => Seed = seed;

        public int Seed { get; }

        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int FactoryCount { get; private set; }

        public static int DirectCount { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .Construct(source => new(ByFactory<Destination>(
                    () => new Destination(FactoryValue(source.Value)))))
                .Members((source, _) => new()
                {
                    Value = FactoryValue(source.Value)
                });

            builder.Map<Source, IDirectDestination>()
                .Construct(source =>
                    new DirectDestination(DirectValue(source.Value)))
                .Members((source, _) => new()
                {
                    Value = DirectValue(source.Value)
                });
        }

        private static int FactoryValue(int value)
        {
            FactoryCount++;
            return value + FactoryCount * 10;
        }

        private static int DirectValue(int value)
        {
            DirectCount++;
            return value + DirectCount * 100;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var factory =
                ((ITypeMapper<Source, Destination>)mapper).Create(
                    new Source { Value = 1 },
                    default(MappingContext));
            var direct =
                ((ITypeMapper<Source, IDirectDestination>)mapper).Create(
                    new Source { Value = 2 },
                    default(MappingContext));

            if (factory.Seed != 11 ||
                factory.Value != 21 ||
                direct.Seed != 102 ||
                direct.Value != 202 ||
                TestMapper.FactoryCount != 2 ||
                TestMapper.DirectCount != 2)
            {
                throw new InvalidOperationException(
                    "An opaque construction body participated in sharing.");
            }
        }
    }
}
