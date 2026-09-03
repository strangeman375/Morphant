namespace Morphant.Generator.UnitTests.MappingSettingsDiagnosticsTests;

[TestFixture]
internal sealed class TupleTests
{
    [Test]
    public void Explicit_ConstructorSelection_is_inapplicable_to_intrinsic_tuple_construction()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed record Source(int Id, string Name);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, (int Id, string Name)>()
                .ConstructorSelection(ConstructorSelection.Greediest);
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single(candidate =>
            candidate.Id == "MORPH0023");

        Assert.Multiple(() =>
        {
            Assert.That(
                MappingSettingsDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("ConstructorSelection"));
            Assert.That(
                MappingSettingsDiagnosticsGeneratorTest.SourceText(
                    diagnostic.AdditionalLocations.Single()),
                Is.EqualTo("(int Id, string Name)"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Setting 'ConstructorSelection' does not apply to this " +
                    "destination type for mapping 'TestCase.Source -> " +
                    "System.ValueTuple<int, string>' in mapper " +
                    "'TestCase.TestMapper'."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
