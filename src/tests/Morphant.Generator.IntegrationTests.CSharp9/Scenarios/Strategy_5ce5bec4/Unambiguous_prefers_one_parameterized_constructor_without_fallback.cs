// Compiled integration scenario: TypeMapperConstructorSelectionTests/StrategyTests::Unambiguous_prefers_one_parameterized_constructor_without_fallback
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0036

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Strategy_5ce5bec4
{
    public sealed class Source
    {
        public int Id { get; init; }
    }

    public sealed class PreferredDestination
    {
        public PreferredDestination()
        {
            Kind = "parameterless";
        }

        public PreferredDestination(int id)
        {
            Kind = "parameterized";
            Id = id;
        }

        public string Kind { get; }

        public int Id { get; }
    }

    public sealed class ParameterlessOnly
    {
        public string Kind { get; } = "parameterless";
    }

    public sealed class AmbiguousDestination
    {
        public AmbiguousDestination()
        {
        }

        public AmbiguousDestination(int id)
        {
        }

        public AmbiguousDestination(string value)
        {
        }
    }

    public sealed class NoFallbackDestination
    {
        public NoFallbackDestination()
        {
            Kind = "parameterless";
        }

        public NoFallbackDestination(string missing)
        {
            Kind = missing;
        }

        public string Kind { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, PreferredDestination>()
                .ConstructorSelection(
                    ConstructorSelection.Unambiguous);
            builder.Map<Source, ParameterlessOnly>()
                .ConstructorSelection(
                    ConstructorSelection.Unambiguous);
            builder.Map<Source, AmbiguousDestination>()
                .ConstructorSelection(
                    ConstructorSelection.Unambiguous);
            builder.Map<Source, NoFallbackDestination>()
                .ConstructorSelection(
                    ConstructorSelection.Unambiguous);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Id = 17 };
            var context = default(MappingContext);
            var preferred =
                ((ITypeMapper<Source, PreferredDestination>)mapper)
                    .Create(source, context);
            var parameterless =
                ((ITypeMapper<Source, ParameterlessOnly>)mapper)
                    .Create(source, context);

            if (preferred.Kind != "parameterized" ||
                preferred.Id != 17 ||
                parameterless.Kind != "parameterless")
            {
                throw new InvalidOperationException(
                    "Unambiguous selected the wrong constructor.");
            }

            ExpectNotSupported(() =>
                ((ITypeMapper<Source, AmbiguousDestination>)mapper)
                    .Create(source, context));
            ExpectNotSupported(() =>
                ((ITypeMapper<Source, NoFallbackDestination>)mapper)
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
                "Unambiguous construction unexpectedly succeeded.");
        }
    }
}
