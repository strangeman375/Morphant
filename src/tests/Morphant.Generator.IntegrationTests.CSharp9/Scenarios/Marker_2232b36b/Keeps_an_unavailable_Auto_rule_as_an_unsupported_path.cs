// Compiled integration scenario: TypeMapperMemberTests/MarkerTests::Keeps_an_unavailable_Auto_rule_as_an_unsupported_path
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Marker_2232b36b
{
    public sealed class Source
    {
        public object Value { get; init; } = new();
    }

    public sealed class Destination
    {
        public string Value { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((_, _) => new()
                {
                    Value = Auto()
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();

            try
            {
                mapper.Create(
                    new Source(),
                    default(MappingContext));
                throw new InvalidOperationException(
                    "An unavailable Auto rule was silently ignored.");
            }
            catch (NotSupportedException exception)
                when (exception.Message ==
                    "A configured Auto member cannot be mapped by convention.")
            {
            }
        }
    }
}
