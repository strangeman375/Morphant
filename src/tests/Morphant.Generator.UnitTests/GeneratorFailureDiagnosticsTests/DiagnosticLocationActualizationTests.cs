using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.GeneratorFailureDiagnosticsTests;

[TestFixture]
internal sealed class DiagnosticLocationActualizationTests
{
#pragma warning disable RS2008 // Test-owned descriptor has no release file.
    private static readonly DiagnosticDescriptor Descriptor = new(
        "TEST0001",
        "Test title",
        "Test message {0}",
        "Test.Category",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Test description.",
        helpLinkUri: "https://example.test/TEST0001",
        customTags: ["TestTag"]);
#pragma warning restore RS2008

    [Test]
    public void Rebinds_locations_and_preserves_reported_metadata()
    {
        const string firstSource = "class First { }";
        const string secondSource = "class Second { }";
        var previousFirst = CSharpSyntaxTree.ParseText(
            firstSource,
            path: "First.cs");
        var previousSecond = CSharpSyntaxTree.ParseText(
            secondSource,
            path: "Second.cs");
        var currentFirst = CSharpSyntaxTree.ParseText(
            firstSource,
            path: "First.cs");
        var currentSecond = CSharpSyntaxTree.ParseText(
            secondSource,
            path: "Second.cs");
        var compilation = CSharpCompilation.Create(
            "Current",
            [currentFirst, currentSecond]);
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add("key", "value");
        var diagnostic = Diagnostic.Create(
            Descriptor,
            previousFirst.GetRoot().GetLocation(),
            [previousSecond.GetRoot().GetLocation()],
            properties,
            "argument");

        var actualized = DiagnosticLocationActualizer.Actualize(
            ImmutableArray.Create(diagnostic),
            compilation,
            CancellationToken.None).Single();

        Assert.Multiple(() =>
        {
            Assert.That(actualized.Id, Is.EqualTo("TEST0001"));
            Assert.That(
                actualized.GetMessage(),
                Is.EqualTo("Test message argument"));
            Assert.That(
                actualized.Severity,
                Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(actualized.WarningLevel, Is.EqualTo(1));
            Assert.That(actualized.IsSuppressed, Is.False);
            Assert.That(actualized.Properties, Is.EqualTo(properties));
            Assert.That(
                actualized.Descriptor.Title.ToString(),
                Is.EqualTo("Test title"));
            Assert.That(
                actualized.Descriptor.Description.ToString(),
                Is.EqualTo("Test description."));
            Assert.That(
                actualized.Descriptor.HelpLinkUri,
                Is.EqualTo("https://example.test/TEST0001"));
            Assert.That(
                actualized.Descriptor.CustomTags,
                Is.EqualTo(new[] { "TestTag" }));
            Assert.That(
                actualized.Location.SourceTree,
                Is.SameAs(currentFirst));
            Assert.That(
                actualized.AdditionalLocations.Single().SourceTree,
                Is.SameAs(currentSecond));
        });
    }

    [Test]
    public void Preserves_a_diagnostic_that_already_uses_the_compilation()
    {
        var tree = CSharpSyntaxTree.ParseText(
            "class Current { }",
            path: "Current.cs");
        var compilation = CSharpCompilation.Create(
            "Current",
            [tree]);
        var diagnostic = Diagnostic.Create(
            Descriptor,
            tree.GetRoot().GetLocation(),
            "argument");

        var actualized = DiagnosticLocationActualizer.Actualize(
            ImmutableArray.Create(diagnostic),
            compilation,
            CancellationToken.None).Single();

        Assert.That(actualized, Is.SameAs(diagnostic));
    }

    [Test]
    public void Rebinds_when_the_diagnostic_span_is_unchanged()
    {
        const string previousSource =
            "class Target { }\nclass TailOne { }";
        const string currentSource =
            "class Target { }\nclass TailTwo { }";
        var previousTree = CSharpSyntaxTree.ParseText(
            previousSource,
            path: "TestCase.cs");
        var currentTree = CSharpSyntaxTree.ParseText(
            currentSource,
            path: "TestCase.cs");
        var target = previousTree.GetRoot()
            .DescendantTokens()
            .Single(token => token.ValueText == "Target");
        var diagnostic = Diagnostic.Create(
            Descriptor,
            target.GetLocation(),
            "argument");

        var actualized = DiagnosticLocationActualizer.Actualize(
            ImmutableArray.Create(diagnostic),
            CSharpCompilation.Create("Current", [currentTree]),
            CancellationToken.None).Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                actualized.Location.SourceTree,
                Is.SameAs(currentTree));
            Assert.That(
                actualized.Location.SourceSpan,
                Is.EqualTo(target.Span));
        });
    }

    [Test]
    public void Drops_a_source_location_that_cannot_be_rebound()
    {
        var previousTree = CSharpSyntaxTree.ParseText(
            "class Previous { }",
            path: "Removed.cs");
        var diagnostic = Diagnostic.Create(
            Descriptor,
            previousTree.GetRoot().GetLocation(),
            "argument");

        var actualized = DiagnosticLocationActualizer.Actualize(
            ImmutableArray.Create(diagnostic),
            CSharpCompilation.Create("Current"),
            CancellationToken.None).Single();

        Assert.That(actualized.Location, Is.EqualTo(Location.None));
    }
}
