using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.QueryVariableNamesTests.Usage;

[TestFixture]
internal sealed partial class QueryVariableNamesTests
{
    [TestCaseSource(nameof(Cases))]
    public void Preserves_query_bindings_and_readable_names(
        string source, string mapper, LanguageVersion version, string lineEnding, bool shifted)
    {
        var input = (shifted ? "// Unrelated edit before the mapper.\n\n" : "") + source;
        var result = GeneratorTestDriver.Run("QueryVariableNames",
            input.ReplaceLineEndings(lineEnding), version);
        var expected = (version == LanguageVersion.CSharp9 ? SurfaceSources : FileScopedSurfaceSources).Append((
            "Morphant.Generated.TypeMapper.Mapper__5593ddcf35e9ee9a09a76d97eece7243.g.cs", mapper));

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.GeneratedSources.Select(item =>
                    (item.HintName, item.SourceText.ToString())),
                Is.EquivalentTo(expected.Select(item =>
                    (item.Item1, NormalizeExpected(item.Item2, lineEnding)))));
        });
    }

    private static string NormalizeExpected(string snapshot, string literalLineEnding)
    {
        var text = SourceText.From(GeneratedSourceText.Normalize(snapshot));
        var root = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp11)).GetRoot();
        // Only literal token text follows the input line endings. Generated
        // trivia remains CRLF, and nested interpolations never overlap edits.
        return text.WithChanges(root.DescendantTokens()
            .Where(token => token.Text.Contains('\n'))
            .Select(token => new TextChange(token.Span, token.Text.ReplaceLineEndings(literalLineEnding))))
            .ToString();
    }

    private static IEnumerable<TestCaseData> Cases()
    {
        var cases = new (string Name, string Source, string Mapper)[]
        {
            ("ManyLocals", ManyLocalsSource, ManyLocalsMapper),
            ("Clauses", ClausesSource, ClausesMapper),
            ("Collisions", CollisionsSource, CollisionsMapper),
            ("Escaped", EscapedSource, EscapedMapper),
            ("Nested", NestedSource, NestedMapper),
            ("Inferred", InferredSource, InferredMapper),
            ("TupleInference", TupleInferenceSource, TupleInferenceMapper),
            ("ConstructUsing", ConstructUsingSource, ConstructUsingMapper),
            ("ResolveUsing", ResolveUsingSource, ResolveUsingMapper),
            ("ResolveInterpolation", ResolveInterpolationSource, ResolveInterpolationMapper),
            ("MembersInterpolation", MembersInterpolationSource, MembersInterpolationMapper),
            ("Interpolation", InterpolationSource, InterpolationMapper),
            ("VerbatimInterpolation", VerbatimInterpolationSource, VerbatimInterpolationMapper),
            ("RawInterpolation", RawInterpolationSource, RawInterpolationMapper),
            ("MultilineVerbatim", MultilineVerbatimSource, MultilineVerbatimMapper),
            ("MultilineRaw", MultilineRawSource, MultilineRawMapper),
            ("LocalInterpolation", LocalInterpolationSource, LocalInterpolationMapper),
            ("AnonymousParameter", AnonymousParameterSource, AnonymousParameterMapper),
            ("TupleParameter", TupleParameterSource, TupleParameterMapper),
            ("AnonymousLocal", AnonymousLocalSource, AnonymousLocalMapper),
            ("TupleLocal", TupleLocalSource, TupleLocalMapper),
            ("AnonymousRange", AnonymousRangeSource, AnonymousRangeMapper),
            ("TupleRange", TupleRangeSource, TupleRangeMapper),
            ("RepeatedInto", RepeatedIntoSource, RepeatedIntoMapper),
            ("SiblingQueries", SiblingQueriesSource, SiblingQueriesMapper),
            ("ShadowedHelper", ShadowedHelperSource, ShadowedHelperMapper),
            ("ExistingSuffix", ExistingSuffixSource, ExistingSuffixMapper),
            ("UnusedSource", UnusedSourceSource, UnusedSourceMapper),
            ("MapperName", MapperNameSource, MapperNameMapper),
            ("JoinContinuation", JoinContinuationSource, JoinContinuationMapper),
            ("ExplicitProjection", ExplicitProjectionSource, ExplicitProjectionMapper),
            ("NestedInterpolation", NestedInterpolationSource, NestedInterpolationMapper),
            ("QueryComment", QueryCommentSource, QueryCommentMapper),
            ("ConstructUsingInterpolation", ConstructUsingInterpolationSource, ConstructUsingInterpolationMapper),
            ("ResolveUsingInterpolation", ResolveUsingInterpolationSource, ResolveUsingInterpolationMapper),
            ("ResolveReplacementInterpolation", ResolveReplacementInterpolationSource, ResolveReplacementInterpolationMapper),
        };
        foreach (var item in cases)
        {
            var version = item.Name is "RawInterpolation" or "MultilineRaw" ? LanguageVersion.CSharp11 : LanguageVersion.CSharp9;
            yield return new TestCaseData(item.Source, item.Mapper, version, "\n", false)
                .SetName(item.Name + "_LF");
            yield return new TestCaseData(item.Source, item.Mapper, version, "\r\n", false)
                .SetName(item.Name + "_CRLF");
            yield return new TestCaseData(item.Source, item.Mapper, version, "\r\n", true)
                .SetName(item.Name + "_after_adjacent_edit");
        }
    }
}
