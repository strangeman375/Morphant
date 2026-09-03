// Compiled integration scenario: TypeMapperNullHandlingTests::Uses_MSBuild_assembly_defaults_and_pair_overrides
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.AssemblySettings.Scenarios.NullHandling
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
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Source, OverrideDestination>()
                .NullSourceHandling(
                    NullSourceHandling.ReturnDestination)
                .NullDestinationHandling(
                    NullDestinationHandling.Create)
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
            var configured =
                (ITypeMapper<Source, Destination>)mapper;

            Expect<NullSourceException>(
                () => configured.Update(null, new Destination()));
            Expect<NullDestinationException>(
                () => configured.Update(new Source(), null));

            var overridden =
                (ITypeMapper<Source, OverrideDestination>)mapper;
            var previous = new OverrideDestination { Value = 43 };
            var preserved = overridden.Update(null, previous);
            var created = overridden.Update(
                new Source { Value = 47 },
                null);

            if (!ReferenceEquals(previous, preserved) || created.Value != 47)
            {
                throw new InvalidOperationException(
                    "Pair null policies did not override the assembly " +
                    "defaults.");
            }
        }

        private static void Expect<TException>(Action action)
            where TException : MappingException
        {
            try
            {
                action();
            }
            catch (TException exception)
                when (exception.Operation == MappingOperation.Update &&
                      exception.SourceType == typeof(Source) &&
                      exception.DestinationType == typeof(Destination))
            {
                return;
            }

            throw new InvalidOperationException(
                $"The assembly setting did not throw " +
                $"{typeof(TException).Name}.");
        }
    }
}
