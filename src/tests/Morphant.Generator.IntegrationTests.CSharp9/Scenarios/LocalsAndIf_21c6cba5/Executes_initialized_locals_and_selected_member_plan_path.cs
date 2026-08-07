// Compiled integration scenario: TypeMapperDeclarativeControlFlowTests/LocalsAndIfTests::Executes_initialized_locals_and_selected_member_plan_path
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.LocalsAndIf_21c6cba5
{
    public sealed class Source
    {
        public int Value { get; init; }

        public bool Negate { get; init; }

        public bool Fail { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }

        public string Path { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int ValueCount { get; private set; }

        public static int FailureCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, previous) =>
                {
                    const int factor = 2;
                    var value = TrackValue(source.Value * factor);

                    if (source.Fail)
                    {
                        throw BuildFailure();
                    }

                    if (source.Negate)
                    {
                        return new()
                        {
                            Value = -value,
                            Path = previous.HasValue
                                ? "negative-update"
                                : "negative-create"
                        };
                    }

                    return new()
                    {
                        Value = value,
                        Path = previous.HasValue
                            ? "positive-update"
                            : "positive-create"
                    };
                });

        private static int TrackValue(int value)
        {
            ValueCount++;
            return value;
        }

        private static Exception BuildFailure()
        {
            FailureCount++;
            return new InvalidOperationException("selected");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var context = default(MappingContext);
            var positive = mapper.Create(
                new Source { Value = 3 },
                context);
            var previous = new Destination();
            var negative = mapper.Update(
                new Source { Value = 4, Negate = true },
                previous,
                context);

            if (positive.Value != 6 ||
                positive.Path != "positive-create" ||
                !ReferenceEquals(previous, negative) ||
                negative.Value != -8 ||
                negative.Path != "negative-update" ||
                TestMapper.ValueCount != 2 ||
                TestMapper.FailureCount != 0)
            {
                throw new InvalidOperationException(
                    $"The selected member path was lowered incorrectly: " +
                    $"positive=({positive.Value},{positive.Path}), " +
                    $"negative=({negative.Value},{negative.Path}), " +
                    $"same={ReferenceEquals(previous, negative)}, " +
                    $"counts=({TestMapper.ValueCount}," +
                    $"{TestMapper.FailureCount}).");
            }

            try
            {
                mapper.Create(
                    new Source { Value = 5, Fail = true },
                    context);
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "selected" &&
                      TestMapper.ValueCount == 3 &&
                      TestMapper.FailureCount == 1)
            {
                return;
            }

            throw new InvalidOperationException(
                "The selected throw path was not preserved.");
        }
    }
}
