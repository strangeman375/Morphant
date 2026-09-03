namespace Morphant.Generator.UnitTests.ConstructionDiagnosticsTests;

[TestFixture]
internal sealed class CanonicalDiagnosticTests
{
    [Test]
    public void Reports_missing_construction_at_Map_for_no_previous_paths()
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

    public interface IDestination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, IDestination>();
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.ConstructionDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0035"));
            Assert.That(
                ConstructionDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Map"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Mapping 'TestCase.Source -> TestCase.IDestination' " +
                    "cannot create a destination. Affected cases: Create; " +
                    "Update without an existing destination."));
            Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_disabled_automatic_selection_with_setting_origin()
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
        public Destination() { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructorSelection(ConstructorSelection.Explicit);
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.ConstructionDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0036"));
            Assert.That(
                ConstructionDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Map"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    ConstructionDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "ConstructorSelection.Explicit" }));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "ConstructorSelection.Explicit cannot select a " +
                    "constructor for mapping 'TestCase.Source -> " +
                    "TestCase.Destination': destination construction must be " +
                    "configured explicitly."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_invalid_Auto_at_the_marker_with_constructor_location()
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
        public Destination(int value) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(Auto()));
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.ConstructionDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0037"));
            Assert.That(
                ConstructionDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Auto"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    ConstructionDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "Destination" }));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Rule for constructor parameter 'value' is invalid in " +
                    "mapping 'TestCase.Source -> TestCase.Destination': " +
                    "Auto could not find exactly one compatible source " +
                    "member."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_previous_for_only_the_reachable_no_previous_paths()
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

    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Resolve((source, previous) => previous);
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.ConstructionDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0038"));
            Assert.That(
                ConstructionDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("previous"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "'previous' is unavailable in mapping 'TestCase.Source " +
                    "-> TestCase.Destination'. Affected cases: Create; " +
                    "Update without an existing destination."));
            Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_the_smallest_default_producer()
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

    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => default!);
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.ConstructionDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0039"));
            Assert.That(
                ConstructionDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("default"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Construct or Resolve returned null or default for " +
                    "mapping 'TestCase.Source -> TestCase.Destination'. " +
                    "Affected cases: Create; Update without an existing " +
                    "destination."));
            Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
