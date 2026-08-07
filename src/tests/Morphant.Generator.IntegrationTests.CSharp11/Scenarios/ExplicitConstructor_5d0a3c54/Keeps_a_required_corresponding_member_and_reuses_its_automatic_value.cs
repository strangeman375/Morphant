// Compiled integration scenario: TypeMapperStructuredConstructTests/ExplicitConstructorTests::Keeps_a_required_corresponding_member_and_reuses_its_automatic_value
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExplicitConstructor_5d0a3c54
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
            builder.Map<Source, Destination>()
                .Construct(_ => new(Auto()));
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source();
            var destination = mapper.Create(
                source,
                default(MappingContext));

            if (destination.ConstructorValue != 61 ||
                destination.Value != 61 ||
                source.Reads != 1)
            {
                throw new InvalidOperationException(
                    "A required corresponding member was not initialized from the shared value.");
            }
        }
    }
}
