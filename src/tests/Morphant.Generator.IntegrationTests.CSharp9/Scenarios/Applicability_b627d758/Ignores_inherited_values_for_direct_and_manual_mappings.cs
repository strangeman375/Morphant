// Compiled integration scenario: TypeMapperConstructorSelectionTests/ApplicabilityTests::Ignores_inherited_values_for_direct_and_manual_mappings
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Applicability_b627d758
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
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.ConstructorSelection(ConstructorSelection.Explicit);

            builder.Map<Source, string>()
                .Construct(source => source.Value.ToString());
            builder.Map<Source, ManualDestination>()
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
            var direct =
                ((ITypeMapper<Source, string>)mapper)
                    .Create(source, context);
            var manual =
                ((ITypeMapper<Source, ManualDestination>)mapper)
                    .Create(source, context);

            if (direct != "17" || manual.Value != 17)
            {
                throw new InvalidOperationException(
                    "Inherited ConstructorSelection affected an inapplicable mapping.");
            }
        }
    }
}
