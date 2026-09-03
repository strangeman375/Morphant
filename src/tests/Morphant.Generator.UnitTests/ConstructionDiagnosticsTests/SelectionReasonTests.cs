namespace Morphant.Generator.UnitTests.ConstructionDiagnosticsTests;

[TestFixture]
internal sealed class SelectionReasonTests
{
    [Test]
    public void Reports_every_shape_and_greediest_reason_stably()
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
        public int Id { get; init; }
        public string Label { get; init; } = string.Empty;
    }

    public sealed class AParameterless
    {
        public AParameterless(int id) { }
    }

    public sealed class BSingle
    {
        public BSingle() { }
        public BSingle(int id) { }
    }

    public sealed class CUnambiguous
    {
        public CUnambiguous(int id) { }
        public CUnambiguous(string label) { }
    }

    public sealed class DLargest
    {
        public DLargest(int id) { }
        public DLargest(string label) { }
    }

    public sealed class EGreediestNoPlan
    {
        public EGreediestNoPlan(bool enabled) { }
    }

    public sealed class FGreediestTie
    {
        public FGreediestTie(int id) { }
        public FGreediestTie(string label) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, AParameterless>()
                .ConstructorSelection(ConstructorSelection.Parameterless);
            builder.Map<Source, BSingle>()
                .ConstructorSelection(ConstructorSelection.Single);
            builder.Map<Source, CUnambiguous>()
                .ConstructorSelection(ConstructorSelection.Unambiguous);
            builder.Map<Source, DLargest>()
                .ConstructorSelection(ConstructorSelection.Largest);
            builder.Map<Source, EGreediestNoPlan>()
                .ConstructorSelection(ConstructorSelection.Greediest);
            builder.Map<Source, FGreediestTie>()
                .ConstructorSelection(ConstructorSelection.Greediest);
        }
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.ConstructionDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(Enumerable.Repeat("MORPH0036", 6)));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    SelectionMessage(
                        "AParameterless",
                        "Parameterless",
                        "no supported parameterless constructor is available"),
                    SelectionMessage(
                        "BSingle",
                        "Single",
                        "exactly one supported constructor is required, but 2 " +
                        "were found"),
                    SelectionMessage(
                        "CUnambiguous",
                        "Unambiguous",
                        "more than one supported parameterized constructor " +
                        "is available"),
                    SelectionMessage(
                        "DLargest",
                        "Largest",
                        "multiple supported constructors have the largest " +
                        "declared parameter count"),
                    SelectionMessage(
                        "EGreediestNoPlan",
                        "Greediest",
                        "no constructor can be called with automatically " +
                        "mapped arguments"),
                    SelectionMessage(
                        "FGreediestTie",
                        "Greediest",
                        "multiple constructors accept the same highest " +
                        "number of mapped arguments")
                }));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    ConstructionDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.All.EqualTo("Map"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.AdditionalLocations.Count),
                Is.All.EqualTo(1));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    ConstructionDiagnosticsGeneratorTest.SourceText(
                        diagnostic.AdditionalLocations[0])),
                Is.EqualTo(new[]
                {
                    "ConstructorSelection.Parameterless",
                    "ConstructorSelection.Single",
                    "ConstructorSelection.Unambiguous",
                    "ConstructorSelection.Largest",
                    "ConstructorSelection.Greediest",
                    "ConstructorSelection.Greediest"
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_selected_required_parameter_and_binding_failures()
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

    public sealed class ARequired
    {
        public ARequired(int missing) { }
    }

    public sealed class BBinding
    {
        public BBinding(int value) { }
        public BBinding(int value, string label = "") { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ARequired>()
                .ConstructorSelection(ConstructorSelection.Single);
            builder.Map<Source, BBinding>()
                .ConstructorSelection(ConstructorSelection.Largest);
        }
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.ConstructionDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0036", "MORPH0036" }));
            Assert.That(
                diagnostics[0].GetMessage(),
                Does.EndWith(
                    "constructor 'TestCase.ARequired(int missing)' has no " +
                    "compatible source member for required parameter " +
                    "'missing'."));
            Assert.That(
                diagnostics[1].GetMessage(),
                Does.EndWith(
                    "constructor 'TestCase.BBinding(int value, string " +
                    "label)' cannot be called with the mapped arguments."));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.AdditionalLocations.Count),
                Is.All.EqualTo(2));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    ConstructionDiagnosticsGeneratorTest.SourceText(
                        diagnostic.AdditionalLocations[1])),
                Is.EqualTo(new[] { "ARequired", "BBinding" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static string SelectionMessage(
        string destination,
        string strategy,
        string reason)
    {
        return $"ConstructorSelection.{strategy} cannot select a " +
            $"constructor for mapping 'TestCase.Source -> " +
            $"TestCase.{destination}': {reason}.";
    }
}
