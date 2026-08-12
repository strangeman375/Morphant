namespace Morphant.Generator.UnitTests.NestedMappingDiagnosticsTests;

[TestFixture]
internal sealed class MarkerFormsTests
{
    [Test]
    public void Accepts_all_eight_terminal_marker_forms()
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
        public ChildSource First { get; } = new();
        public ChildSource Second { get; } = new();
        public ChildSource Third { get; } = new();
        public ChildSource Fourth { get; } = new();
        public ChildSource Fifth { get; } = new();
        public ChildSource Sixth { get; } = new();
        public ChildSource Seventh { get; } = new();
        public ChildSource Eighth { get; } = new();
        public ChildDestination Previous { get; } = new();
    }

    public sealed class Destination
    {
        public ChildDestination First { get; set; } = new();
        public ChildDestination Second { get; set; } = new();
        public ChildDestination Third { get; set; } = new();
        public ChildDestination Fourth { get; set; } = new();
        public ChildDestination Fifth { get; set; } = new();
        public ChildDestination Sixth { get; set; } = new();
        public ChildDestination Seventh { get; set; } = new();
        public ChildDestination Eighth { get; set; } = new();
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    First = Map(),
                    Second = Map<ChildDestination>(),
                    Third = Map(source.Third),
                    Fourth = Map<ChildDestination>(source.Fourth),
                    Fifth = Create(source.Fifth),
                    Sixth = Create<ChildDestination>(source.Sixth),
                    Seventh = Update(source.Seventh, source.Previous),
                    Eighth = Update<ChildDestination>(
                        source.Eighth,
                        source.Previous)
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.NestedMappingDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [TestCase("null")]
    [TestCase("default")]
    public void Reports_a_source_without_a_natural_static_type(
        string sourceExpression)
    {
        // lang=c#
        var source =
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
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(value => new()
                {
                    Child = Map(SOURCE_EXPRESSION)
                });
    }
}
""".Replace(
            "SOURCE_EXPRESSION",
            sourceExpression,
            StringComparison.Ordinal);

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.NestedMappingDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0044"));
            Assert.That(
                NestedMappingDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo(sourceExpression));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain(
                    "source expression does not have a statically " +
                    "determined type"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Attributes_a_transparent_untyped_alias_to_its_producer()
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
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(value =>
                {
                    var child = Map(null);
                    return new() { Child = child };
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.NestedMappingDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0044"));
            Assert.That(
                NestedMappingDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("null"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
