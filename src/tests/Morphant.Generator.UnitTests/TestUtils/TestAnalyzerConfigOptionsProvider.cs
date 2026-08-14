using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TestAnalyzerConfigOptionsProvider :
    AnalyzerConfigOptionsProvider
{
    private static readonly AnalyzerConfigOptions Empty =
        new TestAnalyzerConfigOptions(
            ImmutableDictionary<string, string>.Empty);

    public TestAnalyzerConfigOptionsProvider(
        ImmutableDictionary<string, string> globalOptions)
    {
        GlobalOptions = new TestAnalyzerConfigOptions(globalOptions);
    }

    public override AnalyzerConfigOptions GlobalOptions { get; }

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return Empty;
    }

    public override AnalyzerConfigOptions GetOptions(
        AdditionalText textFile)
    {
        return Empty;
    }
}

internal sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
{
    private readonly ImmutableDictionary<string, string> _values;

    public TestAnalyzerConfigOptions(
        ImmutableDictionary<string, string> values)
    {
        _values = values;
    }

    public override bool TryGetValue(string key, out string value)
    {
        return _values.TryGetValue(key, out value!);
    }
}
