using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class TypeMapperNullHandlingTests
{
    [Test]
    public void Applies_null_source_policy_before_destination_policy()
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
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class ReturnNullDestination
    {
        public int Value { get; set; }
    }

    public sealed class ReturnDestinationDestination
    {
        public int Value { get; set; }
    }

    public sealed class ThrowDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ReturnNullDestination>()
                .NullSourceHandling(NullSourceHandling.ReturnNull)
                .NullDestinationHandling(NullDestinationHandling.Throw);

            builder.Map<Source, ReturnDestinationDestination>()
                .NullSourceHandling(
                    NullSourceHandling.ReturnDestination)
                .NullDestinationHandling(NullDestinationHandling.Throw);

            builder.Map<Source, ThrowDestination>()
                .NullSourceHandling(NullSourceHandling.Throw)
                .NullDestinationHandling(NullDestinationHandling.Create);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var returnNull =
                (ITypeMapper<Source, ReturnNullDestination>)mapper;
            var returnDestination =
                (ITypeMapper<Source, ReturnDestinationDestination>)mapper;
            var throwMapper =
                (ITypeMapper<Source, ThrowDestination>)mapper;
            var nullPrevious = new ReturnNullDestination();
            var preserved = new ReturnDestinationDestination();

            if (!ReferenceEquals(returnNull.Create(null, context), null) ||
                !ReferenceEquals(
                    returnNull.Update(null, nullPrevious, context),
                    null) ||
                !ReferenceEquals(
                    returnNull.Update(null, null, context),
                    null))
            {
                throw new InvalidOperationException(
                    "ReturnNull did not return the default destination.");
            }

            if (!ReferenceEquals(
                    returnDestination.Create(null, context),
                    null) ||
                !ReferenceEquals(
                    returnDestination.Update(null, preserved, context),
                    preserved) ||
                !ReferenceEquals(
                    returnDestination.Update(null, null, context),
                    null))
            {
                throw new InvalidOperationException(
                    "ReturnDestination did not preserve the destination.");
            }

            ExpectArgumentNull(
                "destination",
                () => returnNull.Update(
                    new Source(),
                    null,
                    context));
            ExpectArgumentNull(
                "source",
                () => throwMapper.Create(null, context));
            ExpectArgumentNull(
                "source",
                () => throwMapper.Update(null, null, context));

            var created = throwMapper.Update(
                new Source { Value = 23 },
                null,
                context);

            if (created.Value != 23)
            {
                throw new InvalidOperationException(
                    "NullDestinationHandling.Create did not construct a result.");
            }
        }

        private static void ExpectArgumentNull(
            string parameterName,
            Action action)
        {
            try
            {
                action();
            }
            catch (ArgumentNullException exception)
                when (exception.ParamName == parameterName)
            {
                return;
            }

            throw new InvalidOperationException(
                "The expected ArgumentNullException was not thrown.");
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Normalizes_nullable_values_and_omits_impossible_checks()
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
    public partial class TestMapper : TypeMapper
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
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }




}
