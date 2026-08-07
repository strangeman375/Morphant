// Compiled integration scenario: TypeMapperConstructorSelectionTests/StrategyTests::Explicit_disables_automatic_construction_only
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Strategy_77765882
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructorSelection(ConstructorSelection.Explicit);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source { Value = 17 };
            var previous = new Destination { Value = 31 };
            var context = default(MappingContext);
            var updated = mapper.Update(source, previous, context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Value != 17)
            {
                throw new InvalidOperationException(
                    "Explicit affected mapping of an existing destination.");
            }

            try
            {
                mapper.Create(source, context);
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Explicit allowed automatic construction.");
        }
    }
}
