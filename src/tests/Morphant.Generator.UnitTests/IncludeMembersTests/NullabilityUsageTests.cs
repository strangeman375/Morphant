namespace Morphant.Generator.UnitTests.IncludeMembersTests;

[TestFixture]
internal sealed class NullabilityUsageTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void The_reference_source_is_non_null_in_either_fluent_order(bool nullable, bool includeFirst)
    {
        var result = IncludeMembersGeneratorTest.Run(Source(nullable, includeFirst, "s.Item"));
        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }

    [Test]
    public void A_nullable_member_inside_the_selector_still_warns_on_dereference()
    {
        var result = IncludeMembersGeneratorTest.Run(Source(true, true, "s.Optional.Item"));
        Assert.That(result.Diagnostics, Is.Empty);
        var warning = result.CompilerWarningsAndErrors.Single();
        Assert.That(warning.Id, Is.EqualTo("CS8602"));
        Assert.That(IncludeMembersGeneratorTest.SourceText(warning.Location), Is.EqualTo("s.Optional"));
    }

    private static string Source(bool nullable, bool includeFirst, string path)
    {
        var include = $".IncludeMembers(s => {path})";
        const string members = ".Members(s => new() { Title = s.Title })";
        return $$"""
#nullable enable
using Morphant;
internal class Item { public int Id { get; set; } }
internal class Source
{
    public Item Item { get; } = new();
    public Source? Optional { get; }
    public string Title { get; } = "";
}
internal class Dto { public int Id { get; set; } public string Title { get; set; } = ""; }
[MorphantMapper] internal partial class Mapper : TypeMapper
{
    protected override void Configure(MapperBuilder b) =>
        b.Map<{{(nullable ? "Source?, Dto?" : "Source, Dto")}}>()
            {{(includeFirst ? include + members : members + include)}};
}
""";
    }
}
