namespace Morphant.Generator.UnitTests.ConstructionDiagnosticsTests;

[TestFixture]
internal sealed class ParameterRuleTests
{
    [Test]
    public void Reports_precise_explicit_and_ByConvention_rule_failures()
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
        public string Text { get; init; } = string.Empty;
    }

    public sealed class AAuto
    {
        public AAuto(int missing) { }
    }

    public sealed class BIgnore
    {
        public BIgnore(int value) { }
    }

    public sealed class CMissingParameter
    {
        public CMissingParameter() { }
        public CMissingParameter(int value) { }
    }

    public sealed class DTypedMarker
    {
        public DTypedMarker(object value) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, AAuto>()
                .Construct(source => new(Auto()));
            builder.Map<Source, BIgnore>()
                .Construct(source => new(Ignore()));
            builder.Map<Source, CMissingParameter>()
                .ConstructorSelection(ConstructorSelection.Parameterless)
                .Construct(source => new(
                    ByConvention(),
                    new() { value = source.Value }));
            builder.Map<Source, DTypedMarker>()
                .Construct(source => new(Value<string>(source.Text)));
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
                Is.EqualTo(Enumerable.Repeat("MORPH0037", 4)));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    ConstructionDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "Auto",
                    "Ignore",
                    "value",
                    "Value"
                }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.Exactly(1).Contains(
                    "Auto could not find exactly one compatible source " +
                    "member"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.Exactly(1).Contains(
                    "Ignore can only omit an optional or params parameter"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.Exactly(1).Contains(
                    "selected constructor " +
                    "'TestCase.CMissingParameter()' does not declare " +
                    "this parameter"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.Exactly(1).Contains(
                    "specified type 'string' does not match " +
                    "parameter type 'object'"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.AdditionalLocations.Count),
                Is.All.EqualTo(1));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Keeps_compiler_binding_errors_and_valid_rules_outside_MORPH0037()
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

    public sealed class ValidDestination
    {
        public ValidDestination(int value, string label = "", params int[] rest)
        {
        }
    }

    public sealed class InvalidDestination
    {
        public InvalidDestination(int value) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ValidDestination>()
                .Construct(source => new(
                    Value(source.Value),
                    Ignore(),
                    Ignore()));
            builder.Map<Source, InvalidDestination>()
                .Construct(source => new(MissingValue));
        }
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.ConstructionDiagnostics, Is.Empty);
            Assert.That(
                result.CompilerWarningsAndErrors.Select(static diagnostic =>
                    diagnostic.Id),
                Does.Contain("CS0103"));
        });
    }
}
