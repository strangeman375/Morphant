using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionDiagnosticsTests;

[TestFixture]
internal sealed class PreviousGuardTests
{
    [TestCase("if (p.TryGetValue(out var d) && d.Id == s.Id) return p;", false)]
    [TestCase("if (p.TryGetValue(out var d)) { if (d.Id == s.Id) return p; }", false)]
    [TestCase("if (!p.TryGetValue(out var d)) return new(s.Id); if (d.Id == s.Id) return p;", false)]
    [TestCase("if (s.TryGetValue(out var d) && d.Id == s.Id) return p;", true)]
    public void Recognizes_only_the_previous_options_guard(string body, bool unavailable)
    {
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
public sealed class Source
{
    public int Id { get; set; }
    public bool TryGetValue(out Destination value) { value = new(Id); return true; }
}
public sealed class Destination
{
    public Destination(int id) => Id = id;
    public int Id { get; }
}
[MorphantMapper]
public sealed partial class Mapper : TypeMapper<Mapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>().Resolve((s, p) =>
        {
            __BODY__
            return new(s.Id);
        });
}
""";
        var result = GeneratorTestDriver.Run("PreviousGuardContracts",
            source.Replace("__BODY__", body), LanguageVersion.CSharp9);
        Assert.That(result.Diagnostics.Select(d => d.Id),
            Is.EqualTo(unavailable ? new[] { "MORPH0038" } : Array.Empty<string>()));
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }
}
