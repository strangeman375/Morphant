using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.NamespaceStyleTests.Usage;

[TestFixture]
internal sealed partial class InterpolatedStringsTests
{
    [TestCase(LanguageVersion.CSharp9, false)]
    [TestCase(LanguageVersion.CSharp9, true)]
    [TestCase(LanguageVersion.CSharp10, false)]
    [TestCase(LanguageVersion.CSharp10, true)]
    public void Preserves_verbatim_interpolations_in_construction_and_members(
        LanguageVersion languageVersion,
        bool crlf)
    {
        Verify(VerbatimSource, languageVersion, crlf,
            languageVersion == LanguageVersion.CSharp9
                ? VerbatimBlockMapper
                : VerbatimFileMapper);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Preserves_raw_interpolations_in_construction_and_members(bool crlf)
    {
        Verify(RawSource, LanguageVersion.CSharp11, crlf, RawFileMapper);
    }

    private static void Verify(
        string source,
        LanguageVersion languageVersion,
        bool crlf,
        string expectedMapper)
    {
        var literalNewLine = crlf ? "\r\n" : "\n";
        var result = GeneratorTestDriver.Run(
            "NamespaceStyles",
            source.ReplaceLineEndings(literalNewLine),
            languageVersion);
        var surfaces = languageVersion == LanguageVersion.CSharp9
            ? BlockSurfaces
            : FileSurfaces;
        var expected = surfaces.Select(item =>
                (item.FileName, GeneratedSourceText.Normalize(item.Content)))
            .Append((
                "Morphant.Generated.TypeMapper.Mapper__915e28902a703dc5a0e72519738253ae.g.cs",
                NormalizeExpectedMapper(expectedMapper, literalNewLine)));

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.GeneratedSources.Select(item =>
                    (item.HintName, item.SourceText.ToString())),
                Is.EquivalentTo(expected));
        });
    }

    private static string NormalizeExpectedMapper(string snapshot, string literalNewLine)
    {
        // Normalize only the readable expected fixture. Actual generated
        // sources are compared verbatim, including every line ending.
        var text = SourceText.From(GeneratedSourceText.Normalize(snapshot));
        var root = CSharpSyntaxTree.ParseText(text,
            new CSharpParseOptions(LanguageVersion.CSharp11)).GetRoot();
        var literals = root.DescendantNodes()
            .OfType<InterpolatedStringExpressionSyntax>();
        return text.WithChanges(literals.Select(literal => new TextChange(
            literal.Span,
            text.ToString(literal.Span).ReplaceLineEndings(literalNewLine)))).ToString();
    }
}
