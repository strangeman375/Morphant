// Compiled integration scenario: TypeMapperNullHandlingTests::Preserves_invalid_policies_independently_and_allows_overrides
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0021

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NullHandlingInvalid_9d7a0308
{
    public readonly struct Source
    {
        public int Value { get; init; }
    }

    public struct InvalidDestination
    {
        public int Value { get; set; }
    }

    public struct SourceOverrideDestination
    {
        public int Value { get; set; }
    }

    public struct FullOverrideDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.NullSourceHandling(
                (NullSourceHandling)int.MaxValue);
            builder.NullDestinationHandling(
                (NullDestinationHandling)int.MaxValue);

            builder.Map<Source, InvalidDestination>();
            builder.Map<Source, SourceOverrideDestination>()
                .NullSourceHandling(NullSourceHandling.ReturnNull);
            builder.Map<Source, FullOverrideDestination>()
                .NullSourceHandling(NullSourceHandling.ReturnNull)
                .NullDestinationHandling(
                    NullDestinationHandling.Create);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 29 };
            var invalid =
                (ITypeMapper<Source, InvalidDestination>)mapper;

            ExpectInvalid(
                () => invalid.Create(source),
                MappingOperation.Create,
                typeof(InvalidDestination),
                "NullSourceHandling");
            ExpectInvalid(
                () => invalid.Update(source, new InvalidDestination()),
                MappingOperation.Update,
                typeof(InvalidDestination),
                "NullSourceHandling");

            var sourceOverride = (ITypeMapper<
                Source,
                SourceOverrideDestination>)mapper;
            var created = sourceOverride.Create(source);

            if (created.Value != 29)
            {
                throw new InvalidOperationException(
                    "The source-policy override did not restore Create.");
            }

            ExpectInvalid(
                () => sourceOverride.Update(
                    source,
                    new SourceOverrideDestination()),
                MappingOperation.Update,
                typeof(SourceOverrideDestination),
                "NullDestinationHandling");

            var full =
                (ITypeMapper<Source, FullOverrideDestination>)mapper;
            var previous = new FullOverrideDestination();
            var updated = full.Update(source, previous);

            if (full.Create(source).Value != 29 ||
                updated.Value != 29)
            {
                throw new InvalidOperationException(
                    "Pair overrides did not hide both invalid null policies.");
            }
        }

        private static void ExpectInvalid(
            Action action,
            MappingOperation operation,
            Type destinationType,
            string setting)
        {
            try
            {
                action();
            }
            catch (MappingConfigurationException exception)
                when (exception.Operation == operation &&
                      exception.SourceType == typeof(Source) &&
                      exception.DestinationType == destinationType &&
                      exception.Reason.Contains(
                          setting + " has an invalid value.",
                          StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                $"An invalid {setting} did not reject {operation}.");
        }
    }
}
