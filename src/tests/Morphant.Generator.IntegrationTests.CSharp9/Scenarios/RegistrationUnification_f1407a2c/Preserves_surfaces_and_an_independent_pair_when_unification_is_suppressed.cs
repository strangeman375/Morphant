// Compiled integration scenario: RegistrationDiagnosticsTests::Preserves_surfaces_and_an_independent_pair_when_unification_is_suppressed
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0014

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RegistrationUnification_f1407a2c
{
    public sealed class Envelope<T> { }

    public sealed record ConflictDestination(int Value);

    public sealed class IndependentSource { }

    public sealed record IndependentDestination(int Value);

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper<TestMapper<T>>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Envelope<T>, ConflictDestination>()
                .Convert(source => new ConflictDestination(1));
            builder.Map<Envelope<int>, ConflictDestination>()
                .Convert(source => new ConflictDestination(2));
            builder.Map<IndependentSource, IndependentDestination>()
                .Convert(source => new IndependentDestination(303));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper<string>();
            var independent =
                (ITypeMapper<IndependentSource, IndependentDestination>)mapper;
            var result = independent.Create(
                new IndependentSource(),
                default(MappingContext));

            if (result.Value != 303)
            {
                throw new InvalidOperationException(
                    "The independent pair did not survive unification recovery.");
            }

            if (mapper is ITypeMapper<Envelope<string>, ConflictDestination> ||
                mapper is ITypeMapper<Envelope<int>, ConflictDestination>)
            {
                throw new InvalidOperationException(
                    "A unifiable executable contract was generated.");
            }
        }
    }
}
