using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.ActualizationTests;

[TestFixture]
internal sealed class RenameLifecycleActualizationTests
{
    [Test]
    public void Replaces_old_hints_after_file_and_symbol_renames()
    {
        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            ExecutableStep(
                "initial symbols",
                [SourceFile("Initial/TestCase.cs", InitialSource)],
                InitialHints,
                "Initial.Scenario"),
            ExecutableStep(
                "source file moved without a semantic change",
                [SourceFile("Moved/TestCase.cs", InitialSource)],
                InitialHints,
                "Initial.Scenario"),
            ExecutableStep(
                "namespace and mapped symbols renamed",
                [SourceFile("Moved/TestCase.cs", RenamedSource)],
                RenamedHints,
                "Renamed.Scenario"),
            ExecutableStep(
                "original names and path restored",
                [SourceFile("Initial/TestCase.cs", InitialSource)],
                InitialHints,
                "Initial.Scenario"));
    }

    private static readonly string[] InitialHints =
    [
        "Morphant.Generated.Construction.Destination__59053fd17e26306a849ae9f6114f73e9.g.cs",
        "Morphant.Generated.MappingExtension.TestMapper.SourceToDestination__bea394c6fc155061bd7f88dc657c4ede.g.cs",
        "Morphant.Generated.Member.Destination__59053fd17e26306a849ae9f6114f73e9.g.cs",
        "Morphant.Generated.MemberExtension.TestMapper.SourceToDestination__bea394c6fc155061bd7f88dc657c4ede.g.cs",
        "Morphant.Generated.TypeMapper.TestMapper__829a5d2eb615686f772dd6b2fe9668b1.g.cs"
    ];

    private static readonly string[] RenamedHints =
    [
        "Morphant.Generated.Construction.DestinationV2__b5eabc011a74308affd3e74295e68a70.g.cs",
        "Morphant.Generated.MappingExtension.MapperV2.SourceV2ToDestinationV2__aaec3066671ee31dc66fc97b7879520b.g.cs",
        "Morphant.Generated.Member.DestinationV2__b5eabc011a74308affd3e74295e68a70.g.cs",
        "Morphant.Generated.MemberExtension.MapperV2.SourceV2ToDestinationV2__aaec3066671ee31dc66fc97b7879520b.g.cs",
        "Morphant.Generated.TypeMapper.MapperV2__40e342d292ba8eb693422ca13026867b.g.cs"
    ];

    // lang=c#
    private const string InitialSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Initial
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
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
            var destination = mapper.Create(new Source { Value = 17 });

            if (destination.Value != 17)
            {
                throw new InvalidOperationException(
                    "Initial mapping was not generated.");
            }
        }
    }
}
""";

    // lang=c#
    private const string RenamedSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Renamed
{
    public sealed class SourceV2
    {
        public int Value { get; init; }
    }

    public sealed class DestinationV2
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class MapperV2 : TypeMapper<MapperV2>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SourceV2, DestinationV2>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<SourceV2, DestinationV2>)new MapperV2();
            var destination = mapper.Create(new SourceV2 { Value = 29 });

            if (destination.Value != 29)
            {
                throw new InvalidOperationException(
                    "Renamed mapping was not generated.");
            }
        }
    }
}
""";
}
