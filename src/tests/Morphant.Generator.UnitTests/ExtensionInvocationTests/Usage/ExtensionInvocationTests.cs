using Microsoft.CodeAnalysis.CSharp;
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
                Is.EquivalentTo(expected.Select(item => (item.Hint, GeneratedSourceText.Normalize(item.Source)))));
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
            ("ObsoleteImport", ObsoleteImportSource, ObsoleteImport9Sources, LanguageVersion.CSharp9),
            ("ObsoleteImport", ObsoleteImportSource, ObsoleteImport10Sources, LanguageVersion.CSharp10),
            ("ImportIsolationReversed", ImportIsolationReversedSource, ImportIsolationReversed9Sources, LanguageVersion.CSharp9),
            ("ImportIsolationReversed", ImportIsolationReversedSource, ImportIsolationReversed10Sources, LanguageVersion.CSharp10),
            ("GlobalNamespace", GlobalNamespaceSource, GlobalNamespace9Sources, LanguageVersion.CSharp9),
            ("GlobalNamespace", GlobalNamespaceSource, GlobalNamespace10Sources, LanguageVersion.CSharp10),
            ("GlobalImports", GlobalImportsSource, GlobalImports10Sources, LanguageVersion.CSharp10),
        };
        foreach (var item in cases)
        foreach (var (ending, edited, label) in new[] { ("\n", false, "LF"), ("\r\n", false, "CRLF"), ("\r\n", true, "IncrementalEdit") })
            yield return new TestCaseData(item.Source, item.Expected, item.Version, ending, edited)
                .SetName(item.Name + "_" + item.Version + "_" + label);
    }
}
