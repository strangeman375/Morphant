using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class ResolveConditionalUsageTests
{
    [TestCase("previous.HasValue && previous.Value.Id == source.Id", false)]
    [TestCase("previous.HasValue && previous.Value.Id == source.Id", true)]
    [TestCase("previous.TryGetValue(out var destination) && destination.Id == source.Id", false)]
    [TestCase("previous.TryGetValue(out var destination) && destination.Id == source.Id", true)]
    public void Separate_returns_target_the_construction_but_a_mixed_conditional_targets_the_destination(
        string condition,
        bool block)
    {
        // lang=c#
        const string template =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
public sealed class Source { public int Id { get; set; } }
public sealed class Destination
{
    public Destination(int id) => Id = id;
    public int Id { get; }
}
[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>().Resolve((source, previous) => __BODY__);
}
""";
        var body = block
            ? "{ if (" + condition + ") return previous.Value; return new(source.Id); }"
            : condition + " ? previous.Value : new(source.Id)";
        var source = template.Replace("__BODY__", body);
        var result = GeneratorTestDriver.Run("ResolveConditionalConsumer", source, LanguageVersion.CSharp9);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(block ? Array.Empty<string>() : new[] { "MORPH0062" }));
            Assert.That(result.CompilerWarningsAndErrors.Select(static diagnostic => diagnostic.Id),
                Is.Empty);
        });
        if (!block)
        {
            Assert.That(result.EffectiveDiagnostics.Single().Location.SourceSpan,
                Is.EqualTo(new TextSpan(source.IndexOf("new(source.Id)", StringComparison.Ordinal),
                    "new(source.Id)".Length)));
        }
    }
}
