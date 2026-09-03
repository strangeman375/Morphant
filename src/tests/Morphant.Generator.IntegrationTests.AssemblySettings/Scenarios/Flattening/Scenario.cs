// Compiled integration scenario: Flattening MSBuild setting
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.AssemblySettings.Scenarios.Flattening
{
    public sealed class Source
    {
        public Details Details { get; init; } = new Details();
    }

    public sealed class Details
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public string? DetailsName { get; set; }
    }

    public sealed class OverrideDestination
    {
        public string? DetailsName { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>(
                    global::Morphant.MappingMode.Create)
                .ConstructorSelection(
                    global::Morphant.ConstructorSelection.Parameterless)
                .MemberSelection(global::Morphant.MemberSelection.Auto)
                .Flattening(global::Morphant.Flattening.Default);

            builder.Map<Source, OverrideDestination>(
                    global::Morphant.MappingMode.Create)
                .ConstructorSelection(
                    global::Morphant.ConstructorSelection.Parameterless)
                .MemberSelection(global::Morphant.MemberSelection.Auto)
                .Flattening(global::Morphant.Flattening.Auto);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source
            {
                Details = new Details { Name = "mapped" }
            };
            var inherited = ((ITypeMapper<Source, Destination>)mapper)
                .Create(source);
            var overridden =
                ((ITypeMapper<Source, OverrideDestination>)mapper)
                .Create(source);

            if (inherited.DetailsName is not null ||
                overridden.DetailsName != "mapped")
            {
                throw new InvalidOperationException(
                    "The MSBuild flattening setting or pair override was " +
                    "ignored.");
            }
        }
    }
}
