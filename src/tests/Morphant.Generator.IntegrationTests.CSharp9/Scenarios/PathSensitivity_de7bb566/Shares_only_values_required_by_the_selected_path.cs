// Compiled integration scenario: TypeMapperEvaluationTests/PathSensitivityTests::Evaluates_only_the_selected_branch_and_reuses_its_value
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PathSensitivity_de7bb566
{
    public sealed class Source
    {
        public int Value { get; init; }

        public bool Alternate { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int seed) => Seed = seed;

        public int Seed { get; }

        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static int ConditionCount { get; private set; }

        public static int PrimaryCount { get; private set; }

        public static int AlternateCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => Select(source.Alternate)
                    ? new(Alternate(source.Value))
                    : new(Primary(source.Value)))
                .Members((source, _) => Select(source.Alternate)
                    ? new()
                    {
                        Value = Alternate(source.Value)
                    }
                    : new()
                    {
                        Value = Primary(source.Value)
                    });

        private static bool Select(bool value)
        {
            ConditionCount++;
            return value;
        }

        private static int Primary(int value)
        {
            PrimaryCount++;
            return value + PrimaryCount * 10;
        }

        private static int Alternate(int value)
        {
            AlternateCount++;
            return value + AlternateCount * 100;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var context = default(MappingContext);
            var primary = mapper.Create(
                new Source { Value = 1 },
                context);
            var alternate = mapper.Create(
                new Source { Value = 2, Alternate = true },
                context);
            var previous = new Destination(9);
            var updated = mapper.Update(
                new Source { Value = 3, Alternate = true },
                previous,
                context);

            if (primary.Seed != 11 ||
                primary.Value != 11 ||
                alternate.Seed != 102 ||
                alternate.Value != 102 ||
                !ReferenceEquals(previous, updated) ||
                updated.Seed != 9 ||
                updated.Value != 203 ||
                TestMapper.ConditionCount != 3 ||
                TestMapper.PrimaryCount != 1 ||
                TestMapper.AlternateCount != 2)
            {
                throw new InvalidOperationException(
                    "The dependency graph was not path-sensitive.");
            }
        }
    }
}
