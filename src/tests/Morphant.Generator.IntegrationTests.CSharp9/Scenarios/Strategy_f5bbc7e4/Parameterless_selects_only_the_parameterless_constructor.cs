// Compiled integration scenario: TypeMapperConstructorSelectionTests/StrategyTests::Parameterless_selects_only_the_parameterless_constructor
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Strategy_f5bbc7e4
{
    public sealed class Source
    {
        public int Id { get; init; }
    }

    public sealed class Destination
    {
        public Destination()
        {
            Kind = "parameterless";
        }

        public Destination(int id)
        {
            Kind = "parameterized";
            Id = id;
        }

        public string Kind { get; }

        public int Id { get; }
    }

    public sealed class WithoutParameterless
    {
        public WithoutParameterless(int id) => Id = id;

        public int Id { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .ConstructorSelection(
                    ConstructorSelection.Parameterless);
            builder.Map<Source, WithoutParameterless>()
                .ConstructorSelection(
                    ConstructorSelection.Parameterless);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Id = 17 };
            var context = default(MappingContext);
            var selected =
                ((ITypeMapper<Source, Destination>)mapper)
                    .Create(source, context);
            var unsupported =
                (ITypeMapper<Source, WithoutParameterless>)mapper;
            var previous = new WithoutParameterless(31);
            var updated = unsupported.Update(
                source,
                previous,
                context);

            if (selected.Kind != "parameterless" ||
                selected.Id != 0 ||
                !ReferenceEquals(previous, updated))
            {
                throw new InvalidOperationException(
                    "Parameterless selected the wrong constructor or affected Update.");
            }

            ExpectNotSupported(() =>
                unsupported.Create(source, context));
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
                "Unavailable parameterless construction did not fail.");
        }
    }
}
