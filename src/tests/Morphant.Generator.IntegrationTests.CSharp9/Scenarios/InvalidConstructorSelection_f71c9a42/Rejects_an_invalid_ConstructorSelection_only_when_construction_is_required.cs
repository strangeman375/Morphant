// Compiled integration scenario: TypeMapperConstructorSelectionTests/ConfigurationTests::Rejects_an_invalid_ConstructorSelection_only_when_construction_is_required
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0021

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InvalidConstructorSelection_f71c9a42
{
    public readonly struct Source
    {
        public int Value { get; init; }
    }

    public struct Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructorSelection((ConstructorSelection)int.MaxValue);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var context = default(MappingContext);
            var source = new Source { Value = 17 };

            try
            {
                _ = mapper.Create(source, context);
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException
                   exception)
                when (exception.Message.Contains(
                    "ConstructorSelection has an invalid value.",
                    StringComparison.Ordinal))
            {
                var updated = mapper.Update(
                    source,
                    new Destination { Value = 41 },
                    context);

                if (updated.Value == 17)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                "An invalid ConstructorSelection did not remain limited " +
                "to the construction path.");
        }
    }
}
