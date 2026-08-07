// Compiled integration scenario: TypeMapperConventionTests/MemberTests::Rejects_creation_when_a_required_member_has_no_convention_value
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.Member_0047f37c
{
    public sealed class Source
    {
    }

    public sealed class Destination
    {
        public required string Name { get; init; }
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
                Name = "preserved"
            };
            var updated = mapper.Update(
                new Source(),
                previous,
                default(MappingContext));

            if (!ReferenceEquals(updated, previous) ||
                updated.Name != "preserved")
            {
                throw new InvalidOperationException(
                    "An existing required value was not preserved.");
            }

            try
            {
                _ = mapper.Create(
                    new Source(),
                    default(MappingContext));
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Creation ignored an unmapped required member.");
        }
    }
}
