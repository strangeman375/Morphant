// Compiled integration scenario: CallbackDiagnosticsTests::Preserves_contracts_and_independent_paths_for_suppressed_structured_method_groups
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0029

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CallbackDiagnosticsRecovery_8c0f0029
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class ConstructDestination
    {
        public int Value { get; set; }
    }

    public sealed class ResolveDestination
    {
        public int Value { get; set; }
    }

    public sealed class MembersDestination
    {
        public int Value { get; set; }
    }

    public sealed class IndependentDestination
    {
        public IndependentDestination(int value) => Value = value;

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConstructDestination>()
                .Construct(BuildConstruction);
            builder.Map<Source, ResolveDestination>()
                .Resolve(BuildResolution);
            builder.Map<Source, MembersDestination>()
                .Members(BuildMembers);
            builder.Map<Source, IndependentDestination>()
                .Convert(source => new IndependentDestination(
                    source?.Value ?? -1));
        }

        private static global::Morphant.Generated.Types.N_Morphant.N_Generator.N_IntegrationTests.N_CSharp9.N_Scenarios.N_CallbackDiagnosticsRecovery__8c0f0029.Plans.ConstructDestinationConstruction
            BuildConstruction(Source source) => new();

        private static global::Morphant.Generated.Types.N_Morphant.N_Generator.N_IntegrationTests.N_CSharp9.N_Scenarios.N_CallbackDiagnosticsRecovery__8c0f0029.Plans.ResolveDestinationConstruction
            BuildResolution(
                Source source,
                Option<ResolveDestination> previous) => new();

        private static global::Morphant.Generated.Types.N_Morphant.N_Generator.N_IntegrationTests.N_CSharp9.N_Scenarios.N_CallbackDiagnosticsRecovery__8c0f0029.Plans.MembersDestinationMembers
            BuildMembers(Source source) => new();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 17 };

            ExpectUnsupported(() =>
                ((ITypeMapper<Source, ConstructDestination>)mapper)
                    .Create(source, default(MappingContext)));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, ResolveDestination>)mapper)
                    .Create(source, default(MappingContext)));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, MembersDestination>)mapper)
                    .Create(source, default(MappingContext)));

            var existing = new ConstructDestination { Value = 23 };
            var updated =
                ((ITypeMapper<Source, ConstructDestination>)mapper)
                .Update(source, existing, default(MappingContext));
            var independent =
                ((ITypeMapper<Source, IndependentDestination>)mapper)
                .Create(source, default(MappingContext));

            if (!ReferenceEquals(existing, updated) ||
                updated.Value != 17 ||
                independent.Value != 17)
            {
                throw new InvalidOperationException(
                    "Suppressed callback diagnostics changed a valid " +
                    "operation or independent mapping pair.");
            }
        }

        private static void ExpectUnsupported(Action action)
        {
            try
            {
                action();
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "An invalid structured callback did not use its typed " +
                "recovery stub.");
        }
    }
}
