using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.MappingCompletenessDiagnosticsTests;

[TestFixture]
internal sealed class PrecedenceAndActualizationTests
{
    [Test]
    public void Nested_failure_suppresses_only_its_target_completeness_warning()
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
        public int Value { get; set; }
    }

    public sealed class Destination
    {
        public string InvalidTarget { get; set; } = string.Empty;
        public int Independent { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.Destination)
                .Members(source => new()
                {
                    InvalidTarget = Map<int>(source.Value)
                });
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0045", "MORPH0048" }));
            Assert.That(
                result.CompletenessDiagnostics.Single().GetMessage(),
                Does.Contain("TestCase.Destination.Independent"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Required_member_error_owns_its_slot_but_not_an_independent_slot()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination
    {
        public required int Required { get; init; }
        public int Independent { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.Destination);
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(
            source,
            languageVersion: LanguageVersion.CSharp11);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0041", "MORPH0048" }));
            Assert.That(
                result.CompletenessDiagnostics.Single().GetMessage(),
                Does.Contain("TestCase.Destination.Independent"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Actualizes_setting_and_effective_member_participation()
    {
        var none = MappingCompletenessDiagnosticsGeneratorTest.Run(
            Source("None", includeRule: false));
        var strict = MappingCompletenessDiagnosticsGeneratorTest.Run(
            Source("Strict", includeRule: false),
            driver: none.Driver);
        var mapped = MappingCompletenessDiagnosticsGeneratorTest.Run(
            Source("Strict", includeRule: true),
            driver: strict.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(none.CompletenessDiagnostics, Is.Empty);
            Assert.That(
                strict.CompletenessDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0047", "MORPH0048" }));
            Assert.That(mapped.CompletenessDiagnostics, Is.Empty);
            Assert.That(mapped.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static string Source(string setting, bool includeRule)
    {
        var rule = includeRule
            ? Environment.NewLine +
              "                .Members(source => new() { " +
              "Unmapped = source.Unused })"
            : string.Empty;

        // lang=c#
        return
$$"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Unused { get; set; }
    }

    public sealed class Destination
    {
        public int Unmapped { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.{{setting}}){{rule}};
    }
}
""";
    }
}
