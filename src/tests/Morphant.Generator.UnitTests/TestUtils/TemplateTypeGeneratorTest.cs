using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TemplateTypeGeneratorTest : CSharpSourceGeneratorTest<TestTemplateTypeGenerator, DefaultVerifier>
{
    private readonly LanguageVersion _languageVersion;

    public TemplateTypeGeneratorTest(LanguageVersion languageVersion)
    {
        _languageVersion = languageVersion;
        TestState.AdditionalReferences.Add(typeof(TypeMapper).Assembly);
    }

    protected override ParseOptions CreateParseOptions()
    {
        return new CSharpParseOptions(_languageVersion, DocumentationMode.Diagnose);
    }
}
