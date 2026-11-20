// Compiled integration scenario: TypeMapperConventionTests/ConstructorTests::Honors_SetsRequiredMembers_without_synthesizing_a_required_assignment
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.Constructor_9cc0ca16
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        [SetsRequiredMembers]
        internal Destination()
        {
            Name = "constructor";
        }

        public required string Name { get; set; }

        public int Value { get; set; }
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
            var result = mapper.Create(
                new Source
                {
                    Value = 59
                },
                default(MappingContext));

            if (result.Name != "constructor" || result.Value != 59)
            {
                throw new InvalidOperationException(
                    "SetsRequiredMembers was not honored.");
            }
        }
    }
}
