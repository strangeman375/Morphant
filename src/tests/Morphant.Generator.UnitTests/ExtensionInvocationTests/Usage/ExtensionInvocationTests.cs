using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ExtensionInvocationTests.Usage;

[TestFixture]
internal sealed partial class ExtensionInvocationTests
{
    [TestCaseSource(nameof(Cases))]
    public void Preserves_extension_syntax_only_when_the_complete_file_keeps_its_semantics(
        string source, (string Hint, string Source)[] expected, LanguageVersion version,
        string lineEnding, bool adjacentEdit)
    {
        source = source.ReplaceLineEndings(lineEnding);
        var result = GeneratorTestDriver.Run("ExtensionInvocation", source, version);
        if (adjacentEdit)
            result = GeneratorTestDriver.Run("ExtensionInvocation",
                "// An unrelated edit before the mapper." + lineEnding + lineEnding + source,
                version, driver: result.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.GeneratedSources.Select(item => (item.HintName, item.SourceText.ToString())),
                Is.EquivalentTo(expected.Select(item => (item.Hint, NormalizeExpected(item.Source, lineEnding)))));
        });
    }

    private static string NormalizeExpected(string source, string literalLineEnding)
    {
        var text = SourceText.From(GeneratedSourceText.Normalize(source));
        var root = CSharpSyntaxTree.ParseText(text).GetRoot();
        // Generated trivia is always CRLF. Only literal token contents retain
        // input line endings, including interpolated verbatim strings.
        return text.WithChanges(root.DescendantTokens().Where(token => token.Text.Contains('\n'))
            .Select(token => new TextChange(token.Span, token.Text.ReplaceLineEndings(literalLineEnding)))).ToString();
    }

    [Test]
    public void Adding_a_query_and_changing_language_version_reconsiders_existing_imports()
    {
        const string queryRegistration = """
            builder.Map<Source, string>().Convert(source =>
            {
                Func<int, int> increment = source!.Increment;
                return string.Join(",", from value in source!.Values select increment(value));
            });
""";
        var initial = GeneratorTestDriver.Run("ExtensionInvocation",
            ImportIsolationSource.Replace(queryRegistration, string.Empty, StringComparison.Ordinal), LanguageVersion.CSharp9);
        var updated = GeneratorTestDriver.Run("ExtensionInvocation", ImportIsolationSource,
            LanguageVersion.CSharp10, driver: initial.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(initial.EffectiveDiagnostics, Is.Empty);
            Assert.That(initial.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(updated.EffectiveDiagnostics, Is.Empty);
            Assert.That(updated.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(updated.GeneratedSources.Select(item => (item.HintName, item.SourceText.ToString())),
                Is.EquivalentTo(ImportIsolation10Sources.Select(item => (item.Hint, GeneratedSourceText.Normalize(item.Source)))));
        });
    }

    private static IEnumerable<TestCaseData> Cases()
    {
        var cases = new (string Name, string Source, (string Hint, string Source)[] Expected, LanguageVersion Version)[]
        {
            ("Construct", ConstructSource, Construct9Sources, LanguageVersion.CSharp9),
            ("Construct", ConstructSource, Construct10Sources, LanguageVersion.CSharp10),
            ("Resolve", ResolveSource, Resolve9Sources, LanguageVersion.CSharp9),
            ("Resolve", ResolveSource, Resolve10Sources, LanguageVersion.CSharp10),
            ("Members", MembersSource, Members9Sources, LanguageVersion.CSharp9),
            ("Members", MembersSource, Members10Sources, LanguageVersion.CSharp10),
            ("Convert", ConvertSource, Convert9Sources, LanguageVersion.CSharp9),
            ("Convert", ConvertSource, Convert10Sources, LanguageVersion.CSharp10),
            ("ConstructUsing", ConstructUsingSource, ConstructUsing9Sources, LanguageVersion.CSharp9),
            ("ConstructUsing", ConstructUsingSource, ConstructUsing10Sources, LanguageVersion.CSharp10),
            ("ResolveUsing", ResolveUsingSource, ResolveUsing9Sources, LanguageVersion.CSharp9),
            ("ResolveUsing", ResolveUsingSource, ResolveUsing10Sources, LanguageVersion.CSharp10),
            ("Chains", ChainsSource, Chains9Sources, LanguageVersion.CSharp9),
            ("Chains", ChainsSource, Chains10Sources, LanguageVersion.CSharp10),
            ("SameNamespace", SameNamespaceSource, SameNamespace9Sources, LanguageVersion.CSharp9),
            ("SameNamespace", SameNamespaceSource, SameNamespace10Sources, LanguageVersion.CSharp10),
            ("SourceScope", SourceScopeSource, SourceScope9Sources, LanguageVersion.CSharp9),
            ("SourceScope", SourceScopeSource, SourceScope10Sources, LanguageVersion.CSharp10),
            ("Conversions", ConversionsSource, Conversions9Sources, LanguageVersion.CSharp9),
            ("Conversions", ConversionsSource, Conversions10Sources, LanguageVersion.CSharp10),
            ("CallerInformation", CallerInformationSource, CallerInformation9Sources, LanguageVersion.CSharp9),
            ("CallerInformation", CallerInformationSource, CallerInformation10Sources, LanguageVersion.CSharp10),
            ("ImportIsolation", ImportIsolationSource, ImportIsolation9Sources, LanguageVersion.CSharp9),
            ("ImportIsolation", ImportIsolationSource, ImportIsolation10Sources, LanguageVersion.CSharp10),
            ("ConditionalFallback", ConditionalFallbackSource, ConditionalFallback9Sources, LanguageVersion.CSharp9),
            ("ConditionalFallback", ConditionalFallbackSource, ConditionalFallback10Sources, LanguageVersion.CSharp10),
            ("ConditionalFallbackMembers", ConditionalFallbackMembersSource, ConditionalFallbackMembers9Sources, LanguageVersion.CSharp9),
            ("ConditionalFallbackMembers", ConditionalFallbackMembersSource, ConditionalFallbackMembers10Sources, LanguageVersion.CSharp10),
            ("ObsoleteImport", ObsoleteImportSource, ObsoleteImport9Sources, LanguageVersion.CSharp9),
            ("ObsoleteImport", ObsoleteImportSource, ObsoleteImport10Sources, LanguageVersion.CSharp10),
            ("ImportIsolationReversed", ImportIsolationReversedSource, ImportIsolationReversed9Sources, LanguageVersion.CSharp9),
            ("ImportIsolationReversed", ImportIsolationReversedSource, ImportIsolationReversed10Sources, LanguageVersion.CSharp10),
            ("GlobalNamespace", GlobalNamespaceSource, GlobalNamespace9Sources, LanguageVersion.CSharp9),
            ("GlobalNamespace", GlobalNamespaceSource, GlobalNamespace10Sources, LanguageVersion.CSharp10),
            ("GlobalImports", GlobalImportsSource, GlobalImports10Sources, LanguageVersion.CSharp10),
            ("Layout", LayoutSource, Layout9Sources, LanguageVersion.CSharp9),
            ("Layout", LayoutSource, Layout10Sources, LanguageVersion.CSharp10),
            ("ImplicitAwait", ImplicitAwaitSource, ImplicitAwait10Sources, LanguageVersion.CSharp10),
            ("ImplicitDeconstruction", ImplicitDeconstructionSource, ImplicitDeconstruction10Sources, LanguageVersion.CSharp10),
            ("ImplicitPattern", ImplicitPatternSource, ImplicitPattern10Sources, LanguageVersion.CSharp10),
            ("ImplicitLoopDeconstruction", ImplicitLoopDeconstructionSource, ImplicitLoopDeconstruction10Sources, LanguageVersion.CSharp10),
            ("ImplicitInitializer", ImplicitInitializerSource, ImplicitInitializer10Sources, LanguageVersion.CSharp10),
        };
        foreach (var item in cases)
        foreach (var (ending, edited, label) in new[] { ("\n", false, "LF"), ("\r\n", false, "CRLF"), ("\r\n", true, "IncrementalEdit") })
            yield return new TestCaseData(item.Source, item.Expected, item.Version, ending, edited)
                .SetName(item.Name + "_" + item.Version + "_" + label);
    }
}
