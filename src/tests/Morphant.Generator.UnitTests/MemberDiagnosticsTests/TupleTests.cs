namespace Morphant.Generator.UnitTests.MemberDiagnosticsTests;

[TestFixture]
internal sealed class TupleTests
{
    [Test]
    public void Reports_a_scalar_System_Tuple_rule_after_a_runtime_factory()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace TestCase
{
    public sealed record Source(int Value, string Text);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Tuple<int, string>>()
                .ConstructUsing(source =>
                    new Tuple<int, string>(source.Value, source.Text))
                .Members(source => new()
                {
                    Item2 = source.Text + ":member"
                });
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.MemberDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0042"));
            Assert.That(
                MemberDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Item2"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    MemberDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "ConstructUsing" }));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Rule for destination member 'element #2 (Item2)' " +
                    "cannot be applied in mapping 'TestCase.Source -> " +
                    "System.Tuple<int, string>': read-only tuple element " +
                    "cannot be assigned after ConstructUsing or " +
                    "ResolveUsing returns. Affected cases: Create; Update " +
                    "without an existing destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
