namespace Morphant.Generator.UnitTests.ConstructionDiagnosticsTests;

[TestFixture]
internal sealed class TupleTests
{
    [Test]
    public void Reports_missing_values_instead_of_defaulting_unnamed_elements()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed record Source(int Value, string Text);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, (int, string)>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(UnmappedMemberValidation.None);
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
                    "Mapping 'TestCase.Source -> " +
                    "System.ValueTuple<int, string>' cannot create a " +
                    "destination. Affected cases: Create."));
            Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
