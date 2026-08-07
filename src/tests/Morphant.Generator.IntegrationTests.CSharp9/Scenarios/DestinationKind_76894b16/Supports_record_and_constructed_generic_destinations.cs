// Compiled integration scenario: TypeMapperConventionTests/DestinationKindTests::Supports_record_and_constructed_generic_destinations
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DestinationKind_76894b16
{
    public sealed class NumberSource
    {
        public int Value { get; init; }
    }

    public sealed class TextSource
    {
        public string Value { get; init; } = string.Empty;
    }

    public sealed record RecordDestination
    {
        public int Value { get; set; }
    }

    public sealed class Box<T>
    {
        public T Value { get; set; } = default!;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<NumberSource, RecordDestination>();
            builder.Map<TextSource, Box<string>>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var recordMapper =
                (ITypeMapper<NumberSource, RecordDestination>)mapper;
            var boxMapper =
                (ITypeMapper<TextSource, Box<string>>)mapper;
            var record = recordMapper.Create(
                new NumberSource
                {
                    Value = 53
                },
                default(MappingContext));
            var box = boxMapper.Create(
                new TextSource
                {
                    Value = "generic"
                },
                default(MappingContext));

            if (record.Value != 53 || box.Value != "generic")
            {
                throw new InvalidOperationException(
                    "A nominal destination kind was not mapped.");
            }
        }
    }
}
