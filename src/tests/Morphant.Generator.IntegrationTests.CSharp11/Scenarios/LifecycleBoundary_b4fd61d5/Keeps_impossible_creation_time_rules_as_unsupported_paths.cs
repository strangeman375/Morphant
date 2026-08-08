// Compiled integration scenario: TypeMapperMemberTests/LifecycleBoundaryTests::Keeps_impossible_creation_time_rules_as_unsupported_paths
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.LifecycleBoundary_b4fd61d5
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class ResultDependentInitDestination
    {
        public ResultDependentInitDestination(int seed)
        {
            Seed = seed;
        }

        public int Seed { get; }

        public int Initial { get; init; }
    }

    public sealed class FactoryInitDestination
    {
        public FactoryInitDestination(int seed)
        {
            Seed = seed;
        }

        public int Seed { get; }

        public int Initial { get; init; }
    }

    public sealed class ResultDependentRequiredDestination
    {
        public ResultDependentRequiredDestination(int seed)
        {
            Seed = seed;
        }

        public int Seed { get; }

        public required string Required { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ResultDependentInitDestination>()
                .Construct(source => new(seed: source.Value))
                .Members((_, _, result) => new()
                {
                    Initial = result.Seed
                });

            builder.Map<Source, FactoryInitDestination>()
                .ConstructUsing(source =>
                    new FactoryInitDestination(source.Value))
                .Members((source, _, _) => new()
                {
                    Initial = source.Value
                });

            builder.Map<Source, ResultDependentRequiredDestination>()
                .Construct(source => new(seed: source.Value))
                .Members((_, _, result) => new()
                {
                    Required = result.Seed.ToString()
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 3 };
            var context = default(MappingContext);

            ExpectUnsupported(() =>
                ((ITypeMapper<Source,
                    ResultDependentInitDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, FactoryInitDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source,
                    ResultDependentRequiredDestination>)mapper)
                .Create(source, context));
        }

        private static void ExpectUnsupported(Action action)
        {
            try
            {
                action();
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "An impossible member lifecycle used a fallback.");
        }
    }
}
