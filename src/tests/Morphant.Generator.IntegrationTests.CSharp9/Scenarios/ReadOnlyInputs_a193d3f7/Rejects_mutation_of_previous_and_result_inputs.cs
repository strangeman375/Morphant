// Compiled integration scenario: TypeMapperNestedMapTests/ReadOnlyInputsTests::Rejects_mutation_of_previous_and_result_inputs
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ReadOnlyInputs_a193d3f7
{
    public sealed record Source(int Value);

    public sealed class ConstructDestination
    {
        public ConstructDestination(int value)
        {
            Value = value;
        }

        public int Value { get; set; }
    }

    public sealed class AssignmentDestination
    {
        public int Value { get; set; }
    }

    public sealed class IncrementDestination
    {
        public int Value { get; set; }
    }

    public sealed class RefDestination
    {
        public int Value;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConstructDestination>()
                .Construct((source, previous) => new(
                    previous.HasValue
                        ? previous.Value.Value = source.Value
                        : source.Value));

            builder.Map<Source, AssignmentDestination>()
                .Members((source, _, result) => new()
                {
                    Value = result.Value = source.Value
                });

            builder.Map<Source, IncrementDestination>()
                .Members((_, _, result) => new()
                {
                    Value = ++result.Value
                });

            builder.Map<Source, RefDestination>()
                .Members((source, _, result) => new()
                {
                    Value = Mutate(ref result.Value, source.Value)
                });
        }

        private static int Mutate(ref int destination, int value)
        {
            destination = value;
            return value;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source(1);

            AssertUnsupported<ConstructDestination>(mapper, source);
            AssertUnsupported<AssignmentDestination>(mapper, source);
            AssertUnsupported<IncrementDestination>(mapper, source);
            AssertUnsupported<RefDestination>(mapper, source);
        }

        private static void AssertUnsupported<TDestination>(
            TestMapper mapper,
            Source source)
        {
            try
            {
                ((ITypeMapper<Source, TDestination>)mapper).Create(
                    source,
                    default(MappingContext));
                throw new InvalidOperationException(
                    "A declarative input mutation was accepted.");
            }
            catch (NotSupportedException)
            {
            }
        }
    }
}
