// Compiled integration scenario: TypeMapperNullHandlingTests::Normalizes_nullable_values_and_omits_impossible_checks
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NullHandling_f3d15fd6
{
    public readonly struct NullableSource
    {
        public int Value { get; init; }
    }

    public struct NullableDestination
    {
        public int Value { get; set; }
    }

    public readonly struct ValueSource
    {
        public int Value { get; init; }
    }

    public struct ValueDestination
    {
        public int Value { get; set; }
    }

    public sealed class ReferenceSource
    {
        public int Value { get; init; }
    }

    public sealed class ReferenceDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<NullableSource?, NullableDestination?>();
            builder.Map<ValueSource, ValueDestination>();
            builder.Map<ReferenceSource, ValueDestination>();
            builder.Map<ValueSource, ReferenceDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var nullable =
                (ITypeMapper<NullableSource?, NullableDestination?>)mapper;
            var values =
                (ITypeMapper<ValueSource, ValueDestination>)mapper;
            var referenceToValue =
                (ITypeMapper<ReferenceSource, ValueDestination>)mapper;
            var valueToReference =
                (ITypeMapper<ValueSource, ReferenceDestination>)mapper;

            if (nullable.Create(null, context).HasValue ||
                nullable.Update(null, new NullableDestination(), context)
                    .HasValue)
            {
                throw new InvalidOperationException(
                    "A null nullable source was not normalized first.");
            }

            var source = new NullableSource { Value = 31 };
            var created = nullable.Create(source, context);
            var createdByUpdate = nullable.Update(source, null, context);
            var updated = nullable.Update(
                source,
                new NullableDestination { Value = 1 },
                context);

            if (created?.Value != 31 ||
                createdByUpdate?.Value != 31 ||
                updated?.Value != 31)
            {
                throw new InvalidOperationException(
                    "Nullable value mapping used the wrong underlying value.");
            }

            var valueResult = values.Update(
                new ValueSource { Value = 47 },
                new ValueDestination(),
                context);

            if (valueResult.Value != 47)
            {
                throw new InvalidOperationException(
                    "Non-nullable value mapping failed.");
            }

            if (referenceToValue.Create(null, context).Value != 0 ||
                referenceToValue.Update(
                    new ReferenceSource { Value = 53 },
                    new ValueDestination(),
                    context).Value != 53 ||
                valueToReference.Update(
                    new ValueSource { Value = 59 },
                    null,
                    context).Value != 59)
            {
                throw new InvalidOperationException(
                    "Mixed nullability forms were not handled independently.");
            }
        }
    }
}
