// Compiled integration scenario: TypeMapperConstructorSelectionTests/ApplicabilityTests::Preserves_explicit_map_level_values_as_invalid_state
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0023

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Applicability_1c7ecb32
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class ManualDestination
    {
        public ManualDestination(int value) => Value = value;

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, string>()
                .ConstructorSelection(ConstructorSelection.Default)
                .ConstructUsing(source => source.Value.ToString());
            builder.Map<Source, ManualDestination>()
                .ConstructorSelection(ConstructorSelection.Default)
                .Convert((source, _, _) =>
                    new ManualDestination(source?.Value ?? -1));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 17 };
            var context = default(MappingContext);

            ExpectNotSupported(() =>
                ((ITypeMapper<Source, string>)mapper)
                    .Create(source, context));
            ExpectNotSupported(() =>
                ((ITypeMapper<Source, ManualDestination>)mapper)
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
                "An inapplicable map-level ConstructorSelection was ignored.");
        }
    }
}
