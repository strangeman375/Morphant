// Compiled integration scenario: TypeMapperDeclarativeControlFlowTests/UnsupportedFormsTests::Keeps_local_function_as_invalid
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0031

using Morphant;
using Morphant.Context;
using System;
using System.IO;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnsupportedForms_22c950b1
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
            int Read() => source.Value;
            return new() { Value = Read() };
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
            catch (global::Morphant.Exceptions.MappingConfigurationException exception)
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
