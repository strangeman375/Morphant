// Compiled integration scenario: TypeMapperMappingModeTests::Uses_the_MSBuild_assembly_default_and_pair_override
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.AssemblySettings.Scenarios.MappingMode
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    public sealed class OverrideDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>(
                    global::Morphant.MappingMode.Default)
                .Members(source => new() { Value = source.Value });
            builder.Map<Source, OverrideDestination>(
                    global::Morphant.MappingMode.CreateAndUpdate)
                .ConstructorSelection(
                    global::Morphant.ConstructorSelection.Parameterless)
                .Members(source => new() { Value = source.Value });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 41 };
            var configured =
                (ITypeMapper<Source, Destination>)mapper;
            var previous = new Destination();
            var updated = configured.Update(source, previous);

            if (!ReferenceEquals(previous, updated) || updated.Value != 41)
            {
                throw new InvalidOperationException(
                    "The assembly MappingMode did not enable Update.");
            }

            ExpectCreateToBeDisabled(configured, source);

            var overridden =
                (ITypeMapper<Source, OverrideDestination>)mapper;
            var created = overridden.Create(source);
            var overridePrevious = new OverrideDestination();
            var overrideUpdated = overridden.Update(
                source,
                overridePrevious);

            if (created.Value != 41 ||
                !ReferenceEquals(overridePrevious, overrideUpdated) ||
                overrideUpdated.Value != 41)
            {
                throw new InvalidOperationException(
                    "The pair MappingMode did not override the assembly " +
                    "default.");
            }
        }

        private static void ExpectCreateToBeDisabled(
            ITypeMapper<Source, Destination> mapper,
            Source source)
        {
            try
            {
                mapper.Create(source);
            }
            catch (MappingOperationNotSupportedException exception)
                when (exception.Operation == MappingOperation.Create &&
                      exception.EffectiveMappingMode ==
                      global::Morphant.MappingMode.Update)
            {
                return;
            }

            throw new InvalidOperationException(
                "The assembly MappingMode did not disable Create.");
        }
    }
}
