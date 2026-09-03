// Compiled integration scenario: RegistrationDiagnosticsTests::Executes_only_the_first_suppressed_duplicate_plan
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0013

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RegistrationDuplicate_6fd24b81
{
    public sealed class Source { }

    public sealed record Destination(int Value);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .Convert(source => new Destination(101));
            builder.Map<Source, Destination>()
                .Convert(source => new Destination(202));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var contract = (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source();
            var created = contract.Create(
                source,
                default(MappingContext));
            var updated = contract.Update(
                source,
                new Destination(-1),
                default(MappingContext));

            if (created.Value != 101 || updated.Value != 101)
            {
                throw new InvalidOperationException(
                    "A later duplicate plan replaced or augmented the first.");
            }
        }
    }
}
