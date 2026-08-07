// Compiled integration scenario: TypeMapperConventionTests/ConstructorTests::Does_not_fallback_from_an_ambiguous_constructor_selection
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Constructor_dd34c318
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public Destination()
        {
        }

        public Destination(int value)
        {
            Value = value;
        }

        public Destination(long value)
        {
            Value = (int)value;
        }

        public int Value { get; set; }
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
            var previous = new Destination
            {
                Value = 1
            };
            var updated = mapper.Update(
                new Source
                {
                    Value = 9
                },
                previous,
                default(MappingContext));

            if (!ReferenceEquals(updated, previous) || updated.Value != 9)
            {
                throw new InvalidOperationException(
                    "Update must remain available without construction.");
            }

            try
            {
                _ = mapper.Create(
                    new Source(),
                    default(MappingContext));
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Ambiguous convention construction unexpectedly fell back.");
        }
    }
}
