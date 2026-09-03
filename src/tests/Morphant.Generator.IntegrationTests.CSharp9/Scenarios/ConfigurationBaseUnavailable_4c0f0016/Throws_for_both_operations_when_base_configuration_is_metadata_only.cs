// Compiled integration scenario: ConfigurationDiagnosticsTests::Throws_for_both_operations_when_base_configuration_is_metadata_only
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0016

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;
using Morphant.Generator.UnitTests.TestAssets.ConfigurationBaseUnavailableScenario;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConfigurationBaseUnavailable_4c0f0016
{
    public sealed class Source
    {
        public int Value { get; set; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : MetadataConfigurationBase<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var contract = (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source { Value = 17 };
            var destination = new Destination();

            ExpectConfigurationFailure(() =>
                contract.Create(source, default(MappingContext)));
            ExpectConfigurationFailure(() =>
                contract.Update(source, destination, default(MappingContext)));
        }

        private static void ExpectConfigurationFailure(Action action)
        {
            try
            {
                action();
            }
            catch (MappingConfigurationException exception)
            {
                if (!exception.Reason.Contains(
                        "base mapper",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The unavailable-base recovery reason was lost.");
                }

                return;
            }

            throw new InvalidOperationException(
                "A metadata-only base configuration was executed.");
        }
    }
}
