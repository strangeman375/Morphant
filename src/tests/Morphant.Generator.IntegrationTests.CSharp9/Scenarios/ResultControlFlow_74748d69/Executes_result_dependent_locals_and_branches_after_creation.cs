// Compiled integration scenario: TypeMapperDeclarativeControlFlowTests/ResultControlFlowTests::Executes_result_dependent_locals_and_branches_after_creation
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ResultControlFlow_74748d69
{
    public sealed class Source
    {
        public int Seed { get; init; }

        public int Delta { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int seed)
        {
            Seed = seed;
            Value = seed * 10;
        }

        public int Seed { get; }

        public int Value { get; set; }

        public string Path { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int ObserveCount { get; private set; }

        public static int FailureCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, previous, result) =>
                {
                    var seed = Observe(result.Seed);

                    if (seed < 0)
                    {
                        throw BuildFailure();
                    }

                    if ((seed & 1) == 0)
                    {
                        return new()
                        {
                            Value = result.Value + source.Delta,
                            Path = previous.HasValue
                                ? "even-update"
                                : "even-create"
                        };
                    }

                    return new()
                    {
                        Value = result.Value - source.Delta,
                        Path = previous.HasValue
                            ? "odd-update"
                            : "odd-create"
                    };
                });

        private static int Observe(int value)
        {
            ObserveCount++;
            return value;
        }

        private static Exception BuildFailure()
        {
            FailureCount++;
            return new InvalidOperationException("negative");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var context = default(MappingContext);
            var created = mapper.Create(
                new Source { Seed = 2, Delta = 3 },
                context);
            var previous = new Destination(3);
            var updated = mapper.Update(
                new Source { Seed = 99, Delta = 4 },
                previous,
                context);

            if (created.Seed != 2 ||
                created.Value != 23 ||
                created.Path != "even-create" ||
                !ReferenceEquals(previous, updated) ||
                updated.Seed != 3 ||
                updated.Value != 26 ||
                updated.Path != "odd-update" ||
                TestMapper.ObserveCount != 2 ||
                TestMapper.FailureCount != 0)
            {
                throw new InvalidOperationException(
                    "Result-dependent control flow ran in the wrong phase.");
            }
        }
    }
}
