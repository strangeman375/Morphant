// Compiled integration scenario: TypeMapperConventionTests/ConstructorTests::Evaluates_a_shared_constructor_and_required_member_value_once
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.Constructor_861b2632
{
    public sealed class Source
    {
        private int reads;

        public int Value
        {
            get
            {
                reads++;
                return 61;
            }
        }

        public int Reads => reads;
    }

    public sealed class Destination
    {
        public Destination(int value)
        {
            ConstructorValue = value;
        }

        public int ConstructorValue { get; }

        public required int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source();
            var result = mapper.Create(source, default(MappingContext));

            if (result.ConstructorValue != 61 ||
                result.Value != 61 ||
                source.Reads != 1)
            {
                throw new InvalidOperationException(
                    "A shared convention value was evaluated more than once.");
            }
        }
    }
}
