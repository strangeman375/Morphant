// Compiled integration scenario: TypeMapperStructuredConstructTests/DestinationKindTests::Executes_for_struct_nullable_record_and_generic_destinations
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DestinationKind_9876d9a6
{
    public sealed class Source<T>
    {
        public T Value { get; init; } = default!;
    }

    public readonly struct StructDestination
    {
        public StructDestination(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public sealed record RecordDestination(string Value);

    public sealed class GenericDestination<T>
    {
        public GenericDestination(T value)
        {
            Value = value;
        }

        public T Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source<int>, StructDestination>()
                .Resolve((source, _) => new(source.Value));

            builder.Map<Source<int>, StructDestination?>()
                .Resolve((source, _) => new(source.Value));

            builder.Map<Source<string>, RecordDestination>()
                .Construct(source => new(source.Value));

            builder.Map<Source<string>, GenericDestination<string>>()
                .Construct(source => new(source.Value));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var numberSource = new Source<int> { Value = 17 };
            var textSource = new Source<string> { Value = "mapped" };

            var structMapper =
                (ITypeMapper<Source<int>, StructDestination>)mapper;
            var replacedStruct = structMapper.Update(
                numberSource,
                new StructDestination(31),
                context);

            var nullableMapper =
                (ITypeMapper<Source<int>, StructDestination?>)mapper;
            var createdNullable = nullableMapper.Update(
                numberSource,
                null,
                context);
            var replacedNullable = nullableMapper.Update(
                numberSource,
                new StructDestination(31),
                context);

            var recordMapper =
                (ITypeMapper<Source<string>, RecordDestination>)mapper;
            var createdRecord = recordMapper.Create(textSource, context);
            var previousRecord = new RecordDestination("previous");
            var preservedRecord = recordMapper.Update(
                textSource,
                previousRecord,
                context);

            var genericMapper =
                (ITypeMapper<
                    Source<string>,
                    GenericDestination<string>>)mapper;
            var generic = genericMapper.Create(textSource, context);

            if (replacedStruct.Value != 17 ||
                createdNullable?.Value != 17 ||
                replacedNullable?.Value != 17 ||
                createdRecord.Value != "mapped" ||
                !ReferenceEquals(previousRecord, preservedRecord) ||
                generic.Value != "mapped")
            {
                throw new InvalidOperationException(
                    "Structured Construct changed destination-kind semantics.");
            }
        }
    }
}
