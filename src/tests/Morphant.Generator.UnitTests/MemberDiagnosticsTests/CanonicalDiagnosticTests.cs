using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.MemberDiagnosticsTests;

[TestFixture]
internal sealed class CanonicalDiagnosticTests
{
    [Test]
    public void Reports_explicit_Auto_without_a_source_candidate()
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
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new() { Value = Auto() });
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);
        Assert.That(
            result.MemberDiagnostics.Length,
            Is.EqualTo(1),
            string.Join(
                Environment.NewLine,
                result.EffectiveDiagnostics
                    .Concat(result.CompilerWarningsAndErrors)
                    .Select(static diagnostic =>
                        diagnostic.Id + ": " + diagnostic.GetMessage())));
        var diagnostic = result.MemberDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0040"));
            Assert.That(
                MemberDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Auto"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Member rule for 'Value' in contract " +
                    "'global::Morphant.ITypeMapper<global::TestCase.Source, " +
                    "global::TestCase.Destination>' is invalid: Auto does " +
                    "not resolve a unique readable source member with a " +
                    "warning-free implicit conversion."));
            Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_an_uninitialized_required_member_at_its_declaration()
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
        public required int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit);
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(
            source,
            LanguageVersion.CSharp11);
        var diagnostic = result.MemberDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0041"));
            Assert.That(
                MemberDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Value"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Required destination member 'Value' in contract " +
                    "'global::Morphant.ITypeMapper<global::TestCase.Source, " +
                    "global::TestCase.Destination>' is not initialized on " +
                    "reachable paths: Create, Update without a previous " +
                    "destination."));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    MemberDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "MemberSelection.Explicit" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_init_assignment_after_a_runtime_result_policy()
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
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructUsing(source => new Destination())
                .Members(source => new() { Value = source.Value });
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);
        Assert.That(
            result.MemberDiagnostics.Length,
            Is.EqualTo(1),
            string.Join(
                Environment.NewLine,
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        var diagnostic = result.MemberDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0042"));
            Assert.That(
                MemberDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Value"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    MemberDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "ConstructUsing" }));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Member rule for 'Value' in contract " +
                    "'global::Morphant.ITypeMapper<global::TestCase.Source, " +
                    "global::TestCase.Destination>' cannot be applied: " +
                    "init-only member cannot be assigned after a runtime " +
                    "result policy has returned the result. Reachable " +
                    "paths: Create, Update without a previous destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_the_smallest_default_member_plan_producer()
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
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => default!);
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);
        Assert.That(
            result.MemberDiagnostics.Length,
            Is.EqualTo(1),
            string.Join(
                Environment.NewLine,
                result.EffectiveDiagnostics
                    .Concat(result.CompilerWarningsAndErrors)
                    .Select(static diagnostic =>
                        diagnostic.Id + ": " + diagnostic.GetMessage())));
        var diagnostic = result.MemberDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0043"));
            Assert.That(
                MemberDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("default"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Structured member plan for contract " +
                    "'global::Morphant.ITypeMapper<global::TestCase.Source, " +
                    "global::TestCase.Destination>' cannot be null on " +
                    "reachable paths: Create, Update without a previous " +
                    "destination, Update with a previous destination."));
            Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
