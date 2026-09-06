using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class GeneratedMemberAliasUsageTests
{
    [Test]
    public void Preserves_required_nullable_and_outer_generic_member_contracts()
    {
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
using System.Diagnostics.CodeAnalysis;
public sealed class Source
{
    public string? Clone { get; set; }
    public int T { get; set; }
    public int InnerMembers { get; set; }
}
public class Outer<T>
{
    public sealed class Inner
    {
        [AllowNull] public required string Clone { get; init; }
        public int T { get; set; }
        public int InnerMembers { get; set; }
    }
}
[MorphantMapper]
public sealed partial class Mapper : TypeMapper<Mapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Outer<int>.Inner>()
            .UnmappedMemberValidation(UnmappedMemberValidation.Destination)
            .Members(s => new() { Clone_ = s.Clone, T_ = s.T, InnerMembers_ = Auto() });
}
""";
        var result = GeneratorTestDriver.Run("MemberAliasContracts", source, LanguageVersion.CSharp11);
        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }

    [Test]
    public void Reports_an_unmapped_original_member_when_only_reserved_names_exist()
    {
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
public sealed class Source { }
public sealed class Destination { public int Clone { get; set; } }
[MorphantMapper]
public sealed partial class Mapper : TypeMapper<Mapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>()
            .MemberSelection(MemberSelection.Explicit)
            .UnmappedMemberValidation(UnmappedMemberValidation.Destination);
}
""";
        var result = GeneratorTestDriver.Run("MemberAliasValidation", source, LanguageVersion.CSharp9);
        Assert.That(result.Diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "MORPH0048" }));
        Assert.That(result.Diagnostics.Single().GetMessage(), Does.Contain("Clone"));
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }
}
