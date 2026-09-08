using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.UnitTests.MappingCompletenessDiagnosticsTests;

[TestFixture]
internal sealed class NumericNarrowingTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Narrowing_is_unmapped_and_reported_only_when_validation_is_enabled(bool validate)
    {
        // lang=c#
        const string template =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
public sealed class Source
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}
public sealed class Destination
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, Destination>()__VALIDATION__;
    }
}
""";
        var source = template.Replace("__VALIDATION__", validate
            ? ".UnmappedMemberValidation(UnmappedMemberValidation.Destination)" : "");
        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.EffectiveDiagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(validate ? new[] { "MORPH0048" } : Array.Empty<string>()));
        });
        if (validate)
        {
            var diagnostic = result.EffectiveDiagnostics.Single();
            Assert.Multiple(() =>
            {
                Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
                Assert.That(diagnostic.Location.SourceSpan,
                    Is.EqualTo(new TextSpan(source.IndexOf("Destination>()", StringComparison.Ordinal),
                        "Destination".Length)));
            });
        }
    }
}
