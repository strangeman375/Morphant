// Compiled integration scenario: TypeMapperCreationResultTests/CaptureTests::Requires_direct_Construct_only_for_reachable_creation
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Capture_d45adfd5
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public interface IDestination
    {
        int Value { get; set; }
    }

    public sealed class Destination : IDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, IDestination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, IDestination>)new TestMapper();
            var source = new Source { Value = 7 };
            var context = default(MappingContext);
            var previous = new Destination();
            var updated = mapper.Update(source, previous, context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Value != 7)
            {
                throw new InvalidOperationException(
                    "Existing direct destination was not mapped.");
            }

            try
            {
                mapper.Create(source, context);
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Direct creation silently used automatic construction.");
        }
    }
}
