namespace Morphant.Generator.UnitTests.IncludeMembersTests;

[TestFixture]
internal sealed class NullabilityUsageTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void An_unannotated_registration_gives_a_non_null_source_in_either_fluent_order(
        bool includeFirst)
    {
        var result = IncludeMembersGeneratorTest.Run(
            Source("Source, Dto", includeFirst, "s.Item"));

        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }

    [Test]
    public void Directly_after_an_annotated_registration_the_source_is_annotated()
    {
        var result = IncludeMembersGeneratorTest.Run(
            Source("Source?, Dto?", includeFirst: true, "s.Item"));
        var warning = result.CompilerWarningsAndErrors.Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(warning.Id, Is.EqualTo("CS8602"));
            Assert.That(
                IncludeMembersGeneratorTest.SourceText(warning.Location),
                Is.EqualTo("s"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void A_null_forgiving_source_selects_members_of_an_annotated_registration(
        bool includeFirst)
    {
        var result = IncludeMembersGeneratorTest.Run(
            Source("Source?, Dto?", includeFirst, "s!.Item"));

        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }

    [Test]
    public void A_nullable_member_inside_the_selector_still_warns_on_dereference()
    {
        var result = IncludeMembersGeneratorTest.Run(
            Source("Source, Dto", includeFirst: true, "s.Optional.Item"));
        var warning = result.CompilerWarningsAndErrors.Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(warning.Id, Is.EqualTo("CS8602"));
            Assert.That(
                IncludeMembersGeneratorTest.SourceText(warning.Location),
                Is.EqualTo("s.Optional"));
        });
    }

    private static string Source(string pair, bool includeFirst, string path)
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
        b.Map<{{pair}}>()
            {{(includeFirst ? include + members : members + include)}};
}
""";
    }
}
