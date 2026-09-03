// Compiled integration scenario: RegistrationDiagnosticsTests::Executes_an_independent_plan_when_unavailable_pair_is_suppressed
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0011

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RegistrationUnavailable_2d0c31af
{
    public sealed class Source { }

    public sealed record Destination(int Value);

    public partial class Container
    {
        private sealed class HiddenSource { }

        [MorphantMapper]
        public partial class TestMapper : TypeMapper<TestMapper>
        {
            protected override void Configure(MapperBuilder builder)
            {
                builder.Map<HiddenSource, Destination>();
                builder.Map<Source, Destination>()
                    .Convert(source => new Destination(417));
            }
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var contract = (ITypeMapper<Source, Destination>)
                new Container.TestMapper();
            var source = new Source();
            var created = contract.Create(
                source,
                default(MappingContext));
            var updated = contract.Update(
                source,
                new Destination(-1),
                default(MappingContext));

            if (created.Value != 417 || updated.Value != 417)
            {
                throw new InvalidOperationException(
                    "The independent contract did not survive exclusion of " +
                    "the unavailable pair.");
            }
        }
    }
}
