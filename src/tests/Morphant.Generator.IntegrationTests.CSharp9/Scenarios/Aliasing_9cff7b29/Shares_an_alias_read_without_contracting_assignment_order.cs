// Compiled integration scenario: TypeMapperEvaluationTests/AliasingTests::Reads_an_aliased_source_in_assignment_order
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Aliasing_9cff7b29
{
    public sealed class Mutable
    {
        public Mutable(int value) => Value = value;

        public int Value { get; set; }

        public int Copy { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static int ReadCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Mutable, Mutable>()
                .Construct(source => new(Observe(source.Value)))
                .Members((source, _) => new()
                {
                    Value = Observe(source.Value),
                    Copy = Observe(source.Value)
                });

        private static int Observe(int value)
        {
            ReadCount++;
            return value + 10;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Mutable, Mutable>)
                new TestMapper();
            var value = new Mutable(5);
            var result = mapper.Update(
                value,
                value,
                default(MappingContext));

            if (!ReferenceEquals(value, result) ||
                result.Value != 15 ||
                result.Copy != 25 ||
                TestMapper.ReadCount != 2)
            {
                throw new InvalidOperationException(
                    "The second source read must observe the preceding assignment to the same instance.");
            }
        }
    }
}
