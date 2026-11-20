// Compiled integration scenario: TypeMapperConstructorSelectionTests/GreediestAndLargestTests::Largest_uses_declared_size_and_never_falls_back
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0036

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GreediestAndLargest_2a460e2b
{
    public sealed class Source
    {
        public int Id { get; init; }

        public int Code { get; init; }
    }

    public sealed class LargestDestination
    {
        public LargestDestination(int id)
        {
            Kind = "small";
            Value = id;
        }

        public LargestDestination(
            int code,
            string label = "default",
            params string[] tags)
        {
            Kind = "largest:" + label + ":" + tags.Length;
            Value = code;
        }

        public string Kind { get; }

        public int Value { get; }
    }

    public sealed class NoFallbackDestination
    {
        public NoFallbackDestination(int id)
        {
        }

        public NoFallbackDestination(int code, string missing)
        {
        }
    }

    public sealed class TiedDestination
    {
        public TiedDestination(int id)
        {
        }

        public TiedDestination(string missing)
        {
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, LargestDestination>()
                .ConstructorSelection(ConstructorSelection.Largest);
            builder.Map<Source, NoFallbackDestination>()
                .ConstructorSelection(ConstructorSelection.Largest);
            builder.Map<Source, TiedDestination>()
                .ConstructorSelection(ConstructorSelection.Largest);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Id = 17, Code = 31 };
            var context = default(MappingContext);
            var largest =
                ((ITypeMapper<Source, LargestDestination>)mapper)
                    .Create(source, context);

            if (largest.Kind != "largest:default:0" ||
                largest.Value != 31)
            {
                throw new InvalidOperationException(
                    "Largest did not select by declared parameter count.");
            }

            ExpectNotSupported(() =>
                ((ITypeMapper<Source, NoFallbackDestination>)mapper)
                    .Create(source, context));
            ExpectNotSupported(() =>
                ((ITypeMapper<Source, TiedDestination>)mapper)
                    .Create(source, context));
        }

        private static void ExpectNotSupported(Action action)
        {
            try
            {
                action();
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Largest unexpectedly fell back or resolved a tie.");
        }
    }
}
