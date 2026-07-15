using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TemplateTypeGeneratorTest : CSharpSourceGeneratorTest<TestTemplateTypeGenerator, DefaultVerifier>
{
    public TemplateTypeGeneratorTest()
    {
        TestState.AdditionalReferences.Add(typeof(TypeMapper).Assembly);
    }

    protected override ParseOptions CreateParseOptions()
    {
        return new CSharpParseOptions(LanguageVersion.CSharp9, DocumentationMode.Diagnose);
    }
}
