// Compiled integration scenario: RegistrationDiagnosticsTests::Throws_for_both_operations_of_a_suppressed_unsupported_contract
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0012

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RegistrationUnsupported_31a8b6c2
{
    public sealed class Source { }

    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper<TSource, TDestination> : TypeMapper<TestMapper<TSource, TDestination>>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<TSource, TDestination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper<Source, Destination>();
            var contract = (ITypeMapper<Source, Destination>)mapper;
            var source = new Source();
            var destination = new Destination();

            ExpectConfigurationFailure(() =>
                contract.Create(source, default(MappingContext)));
            ExpectConfigurationFailure(() =>
                contract.Update(
                    source,
                    destination,
                    default(MappingContext)));
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
                        "a root type parameter",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The unsupported-root reason was not preserved.");
                }

                return;
            }

            throw new InvalidOperationException(
                "The unsupported generic contract was executed.");
        }
    }
}
