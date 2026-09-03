namespace Morphant.Generator.UnitTests.NestedMappingDiagnosticsTests;

[TestFixture]
internal sealed class CanonicalDiagnosticTests
{
    [Test]
    public void Reports_an_untyped_nested_source()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class ChildDestination { }
    public sealed class Source { }

    public sealed class Destination
    {
        public ChildDestination? Child { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new() { Child = Map(null) });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);
        Assert.That(
            result.NestedMappingDiagnostics.Length,
            Is.EqualTo(1),
            Diagnostics(result));
        var diagnostic = result.NestedMappingDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0044"));
            Assert.That(
                NestedMappingDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("null"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    NestedMappingDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "Child", "Child" }));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Cannot determine source or destination type for 'Map' " +
                    "in mapping 'TestCase.Source -> TestCase.Destination': " +
                    "source expression has no compile-time type. Affected " +
                    "cases: Create; Update without an existing destination; " +
                    "Update with an existing destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_an_incompatible_generic_nested_result()
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
        public string Text { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    Text = Map<int>(source.Value)
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);
        Assert.That(
            result.NestedMappingDiagnostics.Length,
            Is.EqualTo(1),
            Diagnostics(result));
        var diagnostic = result.NestedMappingDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0045"));
            Assert.That(
                NestedMappingDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("int"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    NestedMappingDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "Text", "Text" }));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Nested mapping result type 'int' cannot be assigned to " +
                    "'string' in mapping 'TestCase.Source -> " +
                    "TestCase.Destination'. Affected cases: Create; Update " +
                    "without an existing destination; Update with an " +
                    "existing destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_an_incompatible_explicit_Update_destination()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class ChildSource { }
    public sealed class ChildDestination { }

    public sealed class Source
    {
        public ChildSource Child { get; } = new();
    }

    public sealed class Destination
    {
        public ChildDestination? Child { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    Child = Update<ChildDestination>(
                        source.Child,
                        new object())
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);
        Assert.That(
            result.NestedMappingDiagnostics.Length,
            Is.EqualTo(1),
            Diagnostics(result));
        var diagnostic = result.NestedMappingDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0046"));
            Assert.That(
                NestedMappingDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("new object()"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    NestedMappingDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[]
                {
                    "Child",
                    "Child",
                    "ChildDestination"
                }));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Destination for nested 'Update' is invalid in mapping " +
                    "'TestCase.Source -> TestCase.Destination': destination " +
                    "type 'object' cannot be assigned to " +
                    "'TestCase.ChildDestination'. Affected cases: Create; " +
                    "Update without an existing destination; Update with an " +
                    "existing destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static string Diagnostics(
        NestedMappingDiagnosticsGeneratorResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.EffectiveDiagnostics
                .Concat(result.CompilerWarningsAndErrors)
                .Select(static diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage()));
    }
}
