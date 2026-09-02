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
                    "(int Left, int Top)' of the same underlying mapping " +
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
    public void Detects_a_nested_nullable_annotation_difference()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System.Collections.Generic;
using Morphant;

namespace TestCase;

public sealed class Source { }

[MorphantMapper]
public partial class FirstMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<
            Source,
            (List<string?> Values, int Count)>();
}

[MorphantMapper]
public partial class SecondMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<
            Source,
            (List<string> Values, int Count)>();
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single(candidate =>
            candidate.Id == "MORPH0056");

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain("List<string> Values"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain("List<string?> Values"));
            Assert.That(
                MappingRegistrationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Map"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Detects_a_dynamic_object_presentation_difference()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase;

public sealed class Source { }

[MorphantMapper]
public partial class FirstMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, (dynamic Value, int Count)>();
}

[MorphantMapper]
public partial class SecondMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, (object Value, int Count)>();
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single(candidate =>
            candidate.Id == "MORPH0056");

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain("(object Value, int Count)"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain("(dynamic Value, int Count)"));
            Assert.That(
                MappingRegistrationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Map"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Treats_oblivious_and_non_nullable_forms_as_one_presentation()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase;

public sealed class Source { }

#nullable disable annotations

[MorphantMapper]
public partial class ObliviousMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, (string Value, int Count)>();
}

#nullable enable annotations

[MorphantMapper]
public partial class NonNullableMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, (string Value, int Count)>();
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

    [Test]
    public void Keeps_multifile_authority_stable_when_all_trees_are_recreated()
    {
        var firstFiles = new[]
        {
            new RegistrationSourceFile("Z.Mapper.cs", ConflictMapperSource),
            new RegistrationSourceFile(
                "A.Mapper.cs",
                AuthoritativeMapperSource)
        };
        var reorderedFiles = new[]
        {
            new RegistrationSourceFile(
                "A.Mapper.cs",
                AuthoritativeMapperSource),
            new RegistrationSourceFile("Z.Mapper.cs", ConflictMapperSource)
        };
        var initial = MappingRegistrationGeneratorTest.Run(firstFiles);
        var recreated = MappingRegistrationGeneratorTest.Run(
            reorderedFiles,
            driver: initial.Driver);

        Assert.Multiple(() =>
        {
            AssertAuthority(initial);
            AssertAuthority(recreated);
            Assert.That(initial.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(recreated.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static void AssertAuthority(
        MappingRegistrationGeneratorResult result)
    {
        var diagnostic = result.Diagnostics.Single(candidate =>
            candidate.Id == "MORPH0056");

        Assert.That(
            diagnostic.Location.SourceTree?.FilePath,
            Is.EqualTo("Z.Mapper.cs"));
        Assert.That(
            diagnostic.AdditionalLocations.Single().SourceTree?.FilePath,
            Is.EqualTo("A.Mapper.cs"));
        Assert.That(
            diagnostic.GetMessage(),
            Does.Contain("'(int X, int Y) -> (int Left, int Top)'"));
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

    // lang=c#
    private const string AuthoritativeMapperSource =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public partial class AuthoritativeMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<(int X, int Y), (int Left, int Top)>();
}
""";

    // lang=c#
    private const string ConflictMapperSource =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public partial class ConflictMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<(int A, int B), (int Width, int Height)>();
}
""";
}
