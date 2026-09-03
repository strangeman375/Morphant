// Compiled integration scenario: ConfigurationDiagnosticsTests::Recovers_only_directly_visible_pairs_after_root_flow_escape
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0017

using System;
using System.Linq;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConfigurationRootFlow_4c0f0017
{
    public sealed class VisibleSource { }
    public sealed class VisibleDestination { }
    public sealed class HiddenSource { }
    public sealed class HiddenDestination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            RegisterHidden(builder);
            builder.Map<VisibleSource, VisibleDestination>();
        }

        private static void RegisterHidden(MapperBuilder builder)
        {
            builder.Map<HiddenSource, HiddenDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var contract =
                (ITypeMapper<VisibleSource, VisibleDestination>)mapper;

            ExpectConfigurationFailure(() => contract.Create(
                new VisibleSource(),
                default(MappingContext)));
            ExpectConfigurationFailure(() => contract.Update(
                new VisibleSource(),
                new VisibleDestination(),
                default(MappingContext)));

            if (typeof(TestMapper).GetInterfaces().Any(type =>
                    type == typeof(ITypeMapper<HiddenSource, HiddenDestination>)))
            {
                throw new InvalidOperationException(
                    "A registration hidden behind a helper was guessed.");
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
                        "cannot analyze this Configure method",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The root-flow recovery reason was lost.");
                }

                return;
            }

            throw new InvalidOperationException(
                "An unsupported root-builder flow was executed.");
        }
    }
}
