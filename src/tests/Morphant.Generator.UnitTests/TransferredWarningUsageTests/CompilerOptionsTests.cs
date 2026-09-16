using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TransferredWarningUsageTests;

[TestFixture]
internal sealed class CompilerOptionsTests
{
    [TestCase(ReportDiagnostic.Warn, DiagnosticSeverity.Warning)]
    [TestCase(ReportDiagnostic.Error, DiagnosticSeverity.Error)]
    public void Keeps_convention_warnings_compiler_owned_across_adjacent_mapping_pairs(
        ReportDiagnostic reporting, DiagnosticSeverity severity)
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
    public sealed class Source { public int Value => 10; }
    public sealed class OtherSource { public int Value => 20; }
    public sealed class Destination
    {
        [Obsolete("Legacy constructor.")]
        public Destination() { }
        public int Value { get; set; }
    }
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>().Members(source => new() { Value = source.Value });
            builder.Map<OtherSource, Destination>();
        }
    }
}
""";
        var result = GeneratorTestDriver.Run("TestProject", source, LanguageVersion.CSharp9,
            new Dictionary<string, ReportDiagnostic> { ["CS0618"] = reporting });
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors.Select(diagnostic =>
                (diagnostic.Id, diagnostic.Severity, diagnostic.GetMessage(),
                    diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan))),
                Is.EquivalentTo(new[]
                {
                    ("CS0618", severity, "'Destination.Destination()' is obsolete: 'Legacy constructor.'",
                        "new global::TestCase.Destination()\r\n            {\r\n                Value = source.Value\r\n            }"),
                    ("CS0618", severity, "'Destination.Destination()' is obsolete: 'Legacy constructor.'",
                        "new global::TestCase.Destination()\r\n            {\r\n                Value = source.Value\r\n            }")
                }));
        });
    }
}
