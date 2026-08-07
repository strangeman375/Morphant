// Compiled integration scenario: TypeMapperNullHandlingTests::Applies_null_source_policy_before_destination_policy
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NullHandling_8381d92c
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
