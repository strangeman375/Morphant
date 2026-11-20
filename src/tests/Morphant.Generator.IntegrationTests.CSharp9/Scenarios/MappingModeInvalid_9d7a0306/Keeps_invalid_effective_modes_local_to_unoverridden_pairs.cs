// Compiled integration scenario: TypeMapperMappingModeTests::Keeps_invalid_effective_modes_local_to_unoverridden_pairs
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0021

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MappingModeInvalid_9d7a0306
{
    public readonly struct Source
    {
        public int Value { get; init; }
    }

    public struct InvalidDestination
    {
        public int Value { get; set; }
    }

    public struct OverrideDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.MappingMode((MappingMode)int.MaxValue);
            builder.Map<Source, InvalidDestination>();
            builder.Map<Source, OverrideDestination>(
                MappingMode.CreateAndUpdate);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 23 };
            var invalid =
                (ITypeMapper<Source, InvalidDestination>)mapper;

            ExpectInvalid(
                () => invalid.Create(source),
                MappingOperation.Create);
            ExpectInvalid(
                () => invalid.Update(source, new InvalidDestination()),
                MappingOperation.Update);

            var overridden =
                (ITypeMapper<Source, OverrideDestination>)mapper;
            var created = overridden.Create(source);
            var previous = new OverrideDestination();
            var updated = overridden.Update(source, previous);

            if (created.Value != 23 || updated.Value != 23)
            {
                throw new InvalidOperationException(
                    "A valid pair mode did not hide an invalid mapper mode.");
            }
        }

        private static void ExpectInvalid(
            Action action,
            MappingOperation operation)
        {
            try
            {
                action();
            }
            catch (MappingConfigurationException exception)
                when (exception.Operation == operation &&
                      exception.SourceType == typeof(Source) &&
                      exception.DestinationType ==
                      typeof(InvalidDestination) &&
                      exception.Reason.Contains(
                          "MappingMode has an invalid value.",
                          StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                $"An invalid effective mode did not reject {operation}.");
        }
    }
}
