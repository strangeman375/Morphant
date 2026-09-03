namespace Morphant.Generator.UnitTests.MappingCompletenessDiagnosticsTests;

[TestFixture]
internal sealed class CanonicalDiagnosticTests
{
    [Test]
    public void Reports_one_warning_for_each_unused_source_and_destination_member()
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
        public int Used { get; set; }
        public int Unused { get; set; }
    }

    public sealed class Destination
    {
        public int Used { get; set; }
        public int Unmapped { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
                .Members(source => new() { Used = Value(source.Used) });
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.That(
            result.CompletenessDiagnostics.Length,
            Is.EqualTo(2),
            Diagnostics(result));
        var sourceDiagnostic = result.CompletenessDiagnostics[0];
        var destinationDiagnostic = result.CompletenessDiagnostics[1];

        Assert.Multiple(() =>
        {
            Assert.That(sourceDiagnostic.Id, Is.EqualTo("MORPH0047"));
            Assert.That(
                MappingCompletenessDiagnosticsGeneratorTest.SourceText(
                    sourceDiagnostic.Location),
                Is.EqualTo("Source"));
            Assert.That(
                sourceDiagnostic.AdditionalLocations.Select(
                    MappingCompletenessDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "Unused" }));
            Assert.That(
                sourceDiagnostic.GetMessage(),
                Is.EqualTo(
                    "Source member 'TestCase.Source.Unused' is not " +
                    "used by mapping 'TestCase.Source -> " +
                    "TestCase.Destination'."));

            Assert.That(destinationDiagnostic.Id, Is.EqualTo("MORPH0048"));
            Assert.That(
                MappingCompletenessDiagnosticsGeneratorTest.SourceText(
                    destinationDiagnostic.Location),
                Is.EqualTo("Destination"));
            Assert.That(
                destinationDiagnostic.AdditionalLocations.Select(
                    MappingCompletenessDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "Unmapped" }));
            Assert.That(
                destinationDiagnostic.GetMessage(),
                Is.EqualTo(
                    "Destination member " +
                    "'TestCase.Destination.Unmapped' is not mapped " +
                    "by mapping 'TestCase.Source -> TestCase.Destination'."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static string Diagnostics(
        MappingCompletenessDiagnosticsGeneratorResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.EffectiveDiagnostics
                .Concat(result.CompilerWarningsAndErrors)
                .Select(static diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage()));
    }
}
