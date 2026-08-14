// Compiled integration scenario: CallbackDiagnosticsTests::Suppressed_grammar_mutation_and_marker_failures_do_not_escape
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0031

using Morphant;
using Morphant.Context;
using System;
using System.IO;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CallbackDiagnosticsUnsupportedForms_9d7a0205
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
            catch (global::Morphant.Exceptions.MappingConfigurationException exception)
                when (exception.Message.Contains(
                    "This statement is not supported in Construct, Resolve, or Members.",
                    StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                "Unsupported declarative grammar was executed.");
        }
    }
}
