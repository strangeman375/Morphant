// Compiled integration scenario: TypeMapperConventionTests/DestinationKindTests::Supports_value_and_nullable_value_destination_lifecycles
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DestinationKind_4c9e5873
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public struct Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Source, Destination?>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source
            {
                Value = 29
            };
            var valueMapper =
                (ITypeMapper<Source, Destination>)mapper;
            var nullableMapper =
                (ITypeMapper<Source, Destination?>)mapper;
            var previous = new Destination
            {
                Value = 3
            };
            var created = valueMapper.Create(
                source,
                default(MappingContext));
            var updated = valueMapper.Update(
                source,
                previous,
                default(MappingContext));
            var nullableCreated = nullableMapper.Create(
                source,
                default(MappingContext));
            var nullableUpdated = nullableMapper.Update(
                source,
                previous,
                default(MappingContext));

            if (created.Value != 29 ||
                updated.Value != 29 ||
                previous.Value != 3 ||
                nullableCreated?.Value != 29 ||
                nullableUpdated?.Value != 29)
            {
                throw new InvalidOperationException(
                    "Value destination lifecycle produced an unexpected result.");
            }
        }
    }
}
