// Compiled integration scenario: TypeMapperConstructorSelectionTests/StrategyTests::Single_counts_only_accessible_supported_constructors
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0036

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Strategy_82b063f0
{
    public sealed class Source
    {
        public int Id { get; init; }
    }

    public sealed class SingleDestination
    {
        public SingleDestination(int id)
        {
            Id = id;
        }

        public SingleDestination(ref int id)
        {
            Id = id;
        }

        private SingleDestination(string value)
        {
            Id = value.Length;
        }

        public int Id { get; }
    }

    public sealed class MultipleDestination
    {
        public MultipleDestination()
        {
        }

        public MultipleDestination(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, SingleDestination>()
                .ConstructorSelection(ConstructorSelection.Single);
            builder.Map<Source, MultipleDestination>()
                .ConstructorSelection(ConstructorSelection.Single);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Id = 17 };
            var context = default(MappingContext);
            var single =
                ((ITypeMapper<Source, SingleDestination>)mapper)
                    .Create(source, context);

            if (single.Id != 17)
            {
                throw new InvalidOperationException(
                    "Single did not select the only supported constructor.");
            }

            ExpectNotSupported(() =>
                ((ITypeMapper<Source, MultipleDestination>)mapper)
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
                "Multiple supported constructors were treated as Single.");
        }
    }
}
