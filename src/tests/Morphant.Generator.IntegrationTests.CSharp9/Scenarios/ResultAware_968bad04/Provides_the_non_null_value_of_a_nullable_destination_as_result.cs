// Compiled integration scenario: TypeMapperMemberTests/ResultAwareTests::Provides_the_non_null_value_of_a_nullable_destination_as_result
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ResultAware_968bad04
{
    public sealed class Source
    {
        public int Seed { get; init; }

        public int Delta { get; init; }
    }

    public struct Destination
    {
        public Destination(int seed)
        {
            Seed = seed;
            Value = seed * 10;
        }

        public int Seed { get; }

        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination?>()
                .Construct(source => new(seed: source.Seed))
                .Members((source, _, result) => new()
                {
                    Value = result.Value + source.Delta
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination?>)
                new TestMapper();
            var context = default(MappingContext);
            var created = mapper.Create(
                new Source { Seed = 2, Delta = 3 },
                context);
            var updated = mapper.Update(
                new Source { Delta = 4 },
                new Destination(5),
                context);

            if (created is not { Seed: 2, Value: 23 } ||
                updated is not { Seed: 5, Value: 54 })
            {
                throw new InvalidOperationException(
                    "Nullable destination result was not normalized.");
            }
        }
    }
}
