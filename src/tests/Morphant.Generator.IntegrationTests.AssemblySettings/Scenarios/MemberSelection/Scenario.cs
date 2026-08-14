#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.AssemblySettings.Scenarios.MemberSelection
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; } = -1;
    }

    public sealed class OverrideDestination
    {
        public int Value { get; set; } = -1;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>(
                    global::Morphant.MappingMode.Create)
                .ConstructUsing(_ => new Destination())
                .MemberSelection(
                    global::Morphant.MemberSelection.Default);
            builder.Map<Source, OverrideDestination>(
                    global::Morphant.MappingMode.Create)
                .ConstructUsing(_ => new OverrideDestination())
                .MemberSelection(global::Morphant.MemberSelection.Auto);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 59 };
            var configured = ((ITypeMapper<Source, Destination>)mapper)
                .Create(source);
            var overridden = ((ITypeMapper<Source, OverrideDestination>)
                mapper).Create(source);

            if (configured.Value != -1 || overridden.Value != 59)
            {
                throw new InvalidOperationException(
                    "The assembly MemberSelection or its pair override was " +
                    "ignored.");
            }
        }
    }
}
