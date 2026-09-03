// Compiled integration scenario: ConfigurationDiagnosticsTests::Recovers_one_pair_and_executes_an_independent_pair
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0018

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConfigurationPairFlow_4c0f0018
{
    public sealed class BrokenSource
    {
        public int Value { get; set; }
    }

    public sealed class BrokenDestination
    {
        public int Value { get; set; }
    }

    public sealed class IndependentSource
    {
        public int Value { get; set; }
    }

    public sealed class IndependentDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            var broken = builder.Map<BrokenSource, BrokenDestination>();
            _ = broken;
            builder.Map<IndependentSource, IndependentDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var broken =
                (ITypeMapper<BrokenSource, BrokenDestination>)mapper;
            var independent =
                (ITypeMapper<IndependentSource, IndependentDestination>)mapper;

            ExpectConfigurationFailure(() => broken.Create(
                new BrokenSource(),
                default(MappingContext)));
            ExpectConfigurationFailure(() => broken.Update(
                new BrokenSource(),
                new BrokenDestination(),
                default(MappingContext)));

            var created = independent.Create(
                new IndependentSource { Value = 17 },
                default(MappingContext));
            var updated = independent.Update(
                new IndependentSource { Value = 23 },
                created,
                default(MappingContext));

            if (created.Value != 23 ||
                !ReferenceEquals(created, updated))
            {
                throw new InvalidOperationException(
                    "The independent pair did not remain executable.");
            }
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
                        "cannot analyze this mapping configuration",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The pair-flow recovery reason was lost.");
                }

                return;
            }

            throw new InvalidOperationException(
                "An unsupported pair-builder flow was executed.");
        }
    }
}
