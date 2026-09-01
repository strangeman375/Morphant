namespace Morphant.Generator.UnitTests.MappingRegistrationTests;

[TestFixture]
internal sealed class TuplePresentationTests
{
    [Test]
    public void Reports_one_conflict_for_different_presentations_of_one_pair()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public partial class FirstMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<(int X, int Y), (int Left, int Top)>();
}

[MorphantMapper]
public partial class SecondMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<(int A, int B), (int Width, int Height)>();
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single(candidate =>
            candidate.Id == "MORPH0056");

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Mapping 'System.ValueTuple<int, int> -> " +
                    "System.ValueTuple<int, int>' uses tuple presentation " +
                    "'(int A, int B) -> (int Width, int Height)', which " +
                    "conflicts with the presentation '(int X, int Y) -> " +
                    "(int Left, int Top)' of the same physical mapping " +
                    "pair."));
            Assert.That(
                MappingRegistrationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Map"));
            Assert.That(
                MappingRegistrationGeneratorTest.SourceText(
                    diagnostic.AdditionalLocations.Single()),
                Is.EqualTo("Map"));
        });
    }

    [Test]
    public void Shares_identical_recursive_presentations()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public partial class FirstMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<
            ((int Id, string Name) Item, int Count),
            (int Count, (int Id, string Name) Item)>();
}

[MorphantMapper]
public partial class SecondMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<
            ((int Id, string Name) Item, int Count),
            (int Count, (int Id, string Name) Item)>();
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Where(static diagnostic =>
                    diagnostic.Id == "MORPH0056"),
                Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Detects_a_nested_alias_difference()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public partial class FirstMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<
            ((int X, int Y) Point, int Count),
            (int Count, (int X, int Y) Point)>();
}

[MorphantMapper]
public partial class SecondMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<
            ((int Left, int Top) Point, int Count),
            (int Count, (int X, int Y) Point)>();
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Count(static diagnostic =>
                diagnostic.Id == "MORPH0056"),
            Is.EqualTo(1));
    }

    [Test]
    public void Direct_duplicate_keeps_only_the_duplicate_diagnostic()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<(int X, int Y), (int Left, int Top)>();
        builder.Map<(int A, int B), (int Width, int Height)>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics
                .Where(static diagnostic => diagnostic.Id is
                    "MORPH0013" or "MORPH0056")
                .Select(static diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0013" }));
    }

    [Test]
    public void Actualizes_a_presentation_conflict_on_a_reused_driver()
    {
        var initial = MappingRegistrationGeneratorTest.Run(
            BuildActualizationSource(includeConflict: false));
        var conflicting = MappingRegistrationGeneratorTest.Run(
            BuildActualizationSource(includeConflict: true),
            driver: initial.Driver);
        var restored = MappingRegistrationGeneratorTest.Run(
            BuildActualizationSource(includeConflict: false),
            driver: conflicting.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(
                initial.Diagnostics.Where(static diagnostic =>
                    diagnostic.Id == "MORPH0056"),
                Is.Empty);
            Assert.That(
                conflicting.Diagnostics.Count(static diagnostic =>
                    diagnostic.Id == "MORPH0056"),
                Is.EqualTo(1));
            Assert.That(
                restored.Diagnostics.Where(static diagnostic =>
                    diagnostic.Id == "MORPH0056"),
                Is.Empty);
            Assert.That(initial.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(conflicting.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(restored.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static string BuildActualizationSource(bool includeConflict) =>
        ActualizationSource.Replace(
            "__SECOND_MAPPER__",
            includeConflict
                ? SecondMapperSource
                : string.Empty);

    // lang=c#
    private const string ActualizationSource =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public partial class FirstMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<(int X, int Y), (int Left, int Top)>();
}

__SECOND_MAPPER__
""";

    // lang=c#
    private const string SecondMapperSource =
"""
[MorphantMapper]
public partial class SecondMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<(int A, int B), (int Width, int Height)>();
}
""";
}
