namespace Morphant.Generator.UnitTests.IncludeMembersTests;

[TestFixture]
internal sealed class DiagnosticTests
{
    [Test]
    public void Rejects_a_selector_that_is_not_a_member_path()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public Details Details { get; init; } = new();
    }

    public sealed class Details
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public string Name { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .IncludeMembers(source => Select(source));

        private static Details Select(Source source) => source.Details;
    }
}
""";

        var result = IncludeMembersGeneratorTest.Run(source);
        var diagnostic = result.IncludeMembersDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0049"));
            Assert.That(
                IncludeMembersGeneratorTest.SourceText(diagnostic.Location),
                Is.EqualTo("source => Select(source)"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "IncludeMembers is invalid for mapping " +
                    "'TestCase.Source -> TestCase.Destination' in mapper " +
                    "'TestCase.TestMapper': the selector must be an inline " +
                    "property or field path rooted in source."));
            Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Rejects_the_same_included_path_twice()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public Details Details { get; init; } = new();
    }

    public sealed class Details
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public string Name { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .IncludeMembers(source => source.Details)
                .IncludeMembers(source => source.Details);
    }
}
""";

        var result = IncludeMembersGeneratorTest.Run(source);
        var diagnostic = result.IncludeMembersDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0049"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.EndWith(
                    "path 'Details' is included more than once."));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    IncludeMembersGeneratorTest.SourceText),
                Is.EqualTo(new[] { "source => source.Details" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_an_ambiguous_member_from_two_included_scopes()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public Left Left { get; init; } = new();

        public Right Right { get; init; } = new();
    }

    public sealed class Left
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Right
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public string Name { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .IncludeMembers(source => source.Left)
                .IncludeMembers(source => source.Right);
    }
}
""";

        var result = IncludeMembersGeneratorTest.Run(source);
        var diagnostic = result.IncludeMembersDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0050"));
            Assert.That(
                IncludeMembersGeneratorTest.SourceText(diagnostic.Location),
                Is.EqualTo("source => source.Right"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "IncludeMembers is ambiguous for mapping " +
                    "'TestCase.Source -> TestCase.Destination' in mapper " +
                    "'TestCase.TestMapper': member 'Name' is available from " +
                    "'Left', 'Right'. Remove one of the conflicting " +
                    "scopes."));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    IncludeMembersGeneratorTest.SourceText),
                Is.EqualTo(new[] { "source => source.Left" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Rejects_IncludeMembers_combined_with_Convert()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public Details Details { get; init; } = new();
    }

    public sealed class Details
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .IncludeMembers(source => source.Details)
                .Convert(source => new Destination());
    }
}
""";

        var result = IncludeMembersGeneratorTest.Run(source);
        var diagnostic = result.EffectiveDiagnostics
            .Single(candidate => candidate.Id == "MORPH0020");

        Assert.Multiple(() =>
        {
            Assert.That(
                IncludeMembersGeneratorTest.SourceText(diagnostic.Location),
                Is.EqualTo("Convert"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Convert cannot be combined with Construct, Resolve, " +
                    "Members, or IncludeMembers for mapping " +
                    "'TestCase.Source -> TestCase.Destination' in mapper " +
                    "'TestCase.TestMapper'."));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    IncludeMembersGeneratorTest.SourceText),
                Is.EqualTo(new[] { "IncludeMembers", "Convert" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Source_validation_treats_the_path_as_used_and_checks_the_nested_surface()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public Details Details { get; init; } = new();
    }

    public sealed class Details
    {
        public string Used { get; init; } = string.Empty;

        public string Unused { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public string Used { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .IncludeMembers(source => source.Details)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.Source);
    }
}
""";

        var result = IncludeMembersGeneratorTest.Run(source);
        var diagnostics = result.EffectiveDiagnostics
            .Where(candidate => candidate.Id == "MORPH0047")
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Has.Length.EqualTo(1));
            Assert.That(
                IncludeMembersGeneratorTest.SourceText(
                    diagnostics[0].Location),
                Is.EqualTo("Source"));
            Assert.That(
                diagnostics[0].AdditionalLocations.Select(
                    IncludeMembersGeneratorTest.SourceText),
                Is.EqualTo(new[] { "Unused" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
