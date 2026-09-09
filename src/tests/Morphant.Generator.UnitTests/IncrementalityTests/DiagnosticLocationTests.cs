using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class DiagnosticLocationTests
{
    [Test]
    public void Actualizes_primary_and_additional_locations_after_line_mapping_and_file_edits()
    {
        GeneratorDriver? driver = null;
        foreach (var (source, path, mappedPath, firstLine, secondLine) in new[]
        {
            (Source, "Mapper.cs", "Original.cs", 100, 200),
            (Source.Replace("100", "300").Replace("200", "400")
                .Replace("Original.cs", "Modified.cs"), "Mapper.cs", "Modified.cs", 300, 400),
            ("// inserted before the mapper\n\n" + Source,
                "Moved/Mapper.cs", "Original.cs", 100, 200),
            (Source, "Mapper.cs", "Original.cs", 100, 200)
        })
        {
            var result = GeneratorTestDriver.Run(
                "LocationConsumer",
                [new GeneratorTestSourceFile(path, source)],
                LanguageVersion.CSharp9,
                driver: driver);
            driver = result.Driver;

            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.Diagnostics.Select(static diagnostic =>
                (diagnostic.Id, diagnostic.Severity)),
                Is.EqualTo(new[] { ("MORPH0013", DiagnosticSeverity.Error) }));
            Assert.That(result.GeneratedSources.Select(static generated => generated.HintName),
                Is.EquivalentTo(new[]
                {
                    "Morphant.Generated.Construction.TestCase_Destination.g.cs",
                    "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Destination__TestCase_TestMapper.g.cs",
                    "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs"
                }));

            var diagnostic = result.Diagnostics.Single();
            var currentTree = result.OutputCompilation.SyntaxTrees.Single(tree => tree.FilePath == path);
            AssertLocation(diagnostic.Location, source.LastIndexOf("Map<", StringComparison.Ordinal), secondLine);
            AssertLocation(diagnostic.AdditionalLocations.Single(), source.IndexOf("Map<", StringComparison.Ordinal), firstLine);

            void AssertLocation(Location location, int position, int mappedLine)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(location.SourceTree, Is.SameAs(currentTree));
                    Assert.That(location.SourceSpan, Is.EqualTo(new TextSpan(position, 3)));
                    Assert.That(location.GetMappedLineSpan().Path, Is.EqualTo(mappedPath));
                    Assert.That(location.GetMappedLineSpan().StartLinePosition,
                        Is.EqualTo(new LinePosition(mappedLine - 1, 20)));
                });
            }
        }
    }

    // lang=c#
    private const string Source =
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
        protected override void Configure(MapperBuilder builder)
        {
#line 100 "Original.cs"
            builder.Map<Source, Destination>();
#line 200 "Original.cs"
            builder.Map<Source, Destination>();
#line default
        }
    }
}
""";
}
