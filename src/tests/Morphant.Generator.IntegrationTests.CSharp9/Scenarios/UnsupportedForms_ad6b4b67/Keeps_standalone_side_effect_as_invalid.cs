// Compiled integration scenario: TypeMapperDeclarativeControlFlowTests/UnsupportedFormsTests::Keeps_standalone_side_effect_as_invalid
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;
using System.IO;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnsupportedForms_ad6b4b67
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
                .Members((source, _) =>
                {
            Observe(source.Value);
            return new() { Value = source.Value };
                });

        private static void Observe(int value)
        {
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();

            try
            {
                mapper.Create(
                    new Source { Value = 3 },
                    default(MappingContext));
            }
            catch (NotSupportedException exception)
                when (exception.Message.Contains(
                    "Declarative plan",
                    StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                "Unsupported declarative grammar was executed.");
        }
    }
}
