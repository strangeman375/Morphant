// Compiled integration scenario: TypeMapperConventionTests/UpdateTests::Reuses_the_previous_reference_and_only_applies_post_construction_conventions
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Update_76cdcf49
{
    public sealed class Source
    {
        public int Mutable { get; init; }

        public int CreationOnly { get; init; }
    }

    public sealed class Destination
    {
        public int Mutable { get; set; }

        public int CreationOnly { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var previous = new Destination
            {
                Mutable = 1,
                CreationOnly = 41
            };
            var result = mapper.Update(
                new Source
                {
                    Mutable = 7,
                    CreationOnly = 99
                },
                previous,
                default(MappingContext));

            if (!ReferenceEquals(result, previous) ||
                result.Mutable != 7 ||
                result.CreationOnly != 41)
            {
                throw new InvalidOperationException(
                    "Convention Update produced an unexpected result.");
            }
        }
    }
}
