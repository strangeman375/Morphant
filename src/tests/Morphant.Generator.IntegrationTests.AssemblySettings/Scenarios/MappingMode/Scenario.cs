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

            try
            {
                configured.Create(source);
            }
            catch (MappingOperationNotSupportedException exception)
                when (exception.Operation == MappingOperation.Create &&
                      exception.EffectiveMappingMode ==
                      global::Morphant.MappingMode.Update)
            {
                var overridden =
                    (ITypeMapper<Source, OverrideDestination>)mapper;

                if (overridden.Create(source).Value == 41 &&
                    overridden.Update(
                        source,
                        new OverrideDestination()).Value == 41)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                "The assembly MappingMode or its pair override was ignored.");
        }
    }
}
