using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperStructuredConstructTests;

[TestFixture]
internal sealed class DestinationKindTests
{
    [Test]
    public void Executes_for_struct_nullable_record_and_generic_destinations()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace TestCase
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
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source<int>, StructDestination>()
                .Construct((source, _) => new(source.Value));

            builder.Map<Source<int>, StructDestination?>()
                .Construct((source, _) => new(source.Value));

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
            var replacedStruct = structMapper.Map(
                numberSource,
                new StructDestination(31),
                context);

            var nullableMapper =
                (ITypeMapper<Source<int>, StructDestination?>)mapper;
            var createdNullable = nullableMapper.Map(
                numberSource,
                null,
                context);
            var replacedNullable = nullableMapper.Map(
                numberSource,
                new StructDestination(31),
                context);

            var recordMapper =
                (ITypeMapper<Source<string>, RecordDestination>)mapper;
            var createdRecord = recordMapper.Map(textSource, context);
            var previousRecord = new RecordDestination("previous");

            try
            {
                _ = recordMapper.Map(
                    textSource,
                    previousRecord,
                    context);
                throw new InvalidOperationException(
                    "An immutable source-only Update was accepted.");
            }
            catch (NotSupportedException exception)
                when (exception.Message ==
                    "The declarative Update would inevitably return " +
                    "the previous destination unchanged.")
            {
            }

            var genericMapper =
                (ITypeMapper<
                    Source<string>,
                    GenericDestination<string>>)mapper;
            var generic = genericMapper.Map(textSource, context);

            if (replacedStruct.Value != 17 ||
                createdNullable?.Value != 17 ||
                replacedNullable?.Value != 17 ||
                createdRecord.Value != "mapped" ||
                generic.Value != "mapped")
            {
                throw new InvalidOperationException(
                    "Structured Construct changed destination-kind semantics.");
            }
        }
    }
}
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
