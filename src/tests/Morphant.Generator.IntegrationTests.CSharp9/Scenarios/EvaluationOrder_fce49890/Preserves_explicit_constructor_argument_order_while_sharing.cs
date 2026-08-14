// Compiled integration scenario: TypeMapperEvaluationTests/EvaluationOrderTests::Preserves_constructor_argument_order_when_values_are_reused
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.EvaluationOrder_fce49890
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int first, int shared, int last)
        {
            First = first;
            Shared = shared;
            Last = last;
        }

        public int First { get; }

        public int Shared { get; }

        public int Last { get; }

        public int FirstValue { get; set; }

        public int SharedValue { get; set; }

        public int CombinedValue { get; set; }

        public int NestedValue { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static string Order { get; private set; } = string.Empty;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(
                    Wrap(First(source.Value)),
                    Shared(source.Value),
                    Last(source.Value)))
                .Members((source, _) => new()
                {
                    FirstValue = First(source.Value),
                    SharedValue = Shared(source.Value),
                    CombinedValue = Combine(
                        Preceding(source.Value),
                        Nested(source.Value)),
                    NestedValue = Nested(source.Value)
                });

        private static int First(int value)
        {
            Order += "F";
            return value + 1;
        }

        private static int Shared(int value)
        {
            Order += "S";
            return value + 2;
        }

        private static int Wrap(int value)
        {
            Order += "W";
            return value * 10;
        }

        private static int Last(int value)
        {
            Order += "L";
            return value + 3;
        }

        private static int Preceding(int value)
        {
            Order += "P";
            return value + 4;
        }

        private static int Nested(int value)
        {
            Order += "N";
            return value + 5;
        }

        private static int Combine(int first, int second)
        {
            Order += "C";
            return first + second;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var result = mapper.Create(
                new Source { Value = 10 },
                default(MappingContext));
            var order = TestMapper.Order;
            var constructorOrder =
                order.IndexOf('F') < order.IndexOf('W') &&
                order.IndexOf('W') < order.IndexOf('S') &&
                order.IndexOf('S') < order.IndexOf('L');
            var expressionOrder =
                order.IndexOf('P') < order.IndexOf('N') &&
                order.IndexOf('N') < order.IndexOf('C');

            if (result.First != 110 ||
                result.Shared != 12 ||
                result.Last != 13 ||
                result.FirstValue != 11 ||
                result.SharedValue != 12 ||
                result.CombinedValue != 29 ||
                result.NestedValue != 15 ||
                order.Length != 7 ||
                !constructorOrder ||
                !expressionOrder)
            {
                throw new InvalidOperationException(
                    $"Constructor evaluation order changed: " +
                    $"values=({result.First},{result.Shared}," +
                    $"{result.Last},{result.FirstValue}," +
                    $"{result.SharedValue},{result.CombinedValue}," +
                    $"{result.NestedValue}), " +
                    $"order={TestMapper.Order}.");
            }
        }
    }
}
