using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.QueryVariableNamesTests.Usage;

[TestFixture]
internal sealed partial class QueryVariableNamesTests
{
    [TestCaseSource(nameof(Cases))]
    public void Preserves_query_bindings_and_readable_names(
        string source, string mapper, string lineEnding, bool shifted)
    {
        var input = (shifted ? "// Unrelated edit before the mapper.\n\n" : "") + source;
        var result = GeneratorTestDriver.Run("QueryVariableNames",
            input.ReplaceLineEndings(lineEnding), LanguageVersion.CSharp9);
        var expected = SurfaceSources.Append((
            "Morphant.Generated.TypeMapper.Mapper__5593ddcf35e9ee9a09a76d97eece7243.g.cs", mapper));

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.GeneratedSources.Select(item =>
                    (item.HintName, item.SourceText.ToString())),
                Is.EquivalentTo(expected.Select(item =>
                    (item.Item1, GeneratedSourceText.Normalize(item.Item2)))));
        });
    }

    private static IEnumerable<TestCaseData> Cases()
    {
        var cases = new (string Name, string Source, string Mapper)[]
        {
            ("Clauses", ClausesSource, ClausesMapper),
            ("Collisions", CollisionsSource, CollisionsMapper),
            ("Escaped", EscapedSource, EscapedMapper),
            ("Nested", NestedSource, NestedMapper),
            ("Inferred", InferredSource, InferredMapper),
            ("TupleInference", TupleInferenceSource, TupleInferenceMapper),
            ("ConstructUsing", ConstructUsingSource, ConstructUsingMapper),
            ("ResolveUsing", ResolveUsingSource, ResolveUsingMapper),
        };
        foreach (var item in cases)
        {
            yield return new TestCaseData(item.Source, item.Mapper, "\n", false)
                .SetName(item.Name + "_LF");
            yield return new TestCaseData(item.Source, item.Mapper, "\r\n", false)
                .SetName(item.Name + "_CRLF");
            yield return new TestCaseData(item.Source, item.Mapper, "\r\n", true)
                .SetName(item.Name + "_after_adjacent_edit");
        }
    }
}
