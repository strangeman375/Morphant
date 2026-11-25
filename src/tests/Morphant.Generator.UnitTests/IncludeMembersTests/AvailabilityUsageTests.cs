namespace Morphant.Generator.UnitTests.IncludeMembersTests;

[TestFixture]
internal sealed class AvailabilityUsageTests
{
    [TestCase("b.Map<int, Dto>().IncludeMembers(s => s)", null)]
    [TestCase("b.Map<string, Dto>().IncludeMembers(s => s)", null)]
    [TestCase("b.Map<Kind, Dto>().IncludeMembers(s => s)", null)]
    [TestCase("b.Map<object, Dto>().IncludeMembers(s => s)", null)]
    // Array and delegate sources need Convert even without IncludeMembers.
    [TestCase("b.Map<Order[], Dto>().IncludeMembers(s => s)", "MORPH0035")]
    [TestCase("b.Map<System.Func<Order>, Dto>().IncludeMembers(s => s)", "MORPH0035")]
    [TestCase("b.Map<Order, IDestination>().ConstructUsing(_ => new Implementation()).IncludeMembers(s => s.Inner)", null)]
    public void Is_not_offered_where_it_cannot_affect_conventions(
        string registration,
        string? registrationDiagnostic)
    {
        var result = IncludeMembersGeneratorTest.Run(Source(registration));
        var error = result.CompilerWarningsAndErrors.Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(registrationDiagnostic is null
                    ? Array.Empty<string>()
                    : new[] { registrationDiagnostic }));
            Assert.That(error.Id, Is.EqualTo("CS1061"));
            Assert.That(
                IncludeMembersGeneratorTest.SourceText(error.Location),
                Is.EqualTo("IncludeMembers"));
        });
    }

    [Test]
    public void Selects_from_the_value_of_a_nullable_struct_source()
    {
        var result = IncludeMembersGeneratorTest.Run(
            Source("b.Map<Wrapper?, Dto>().IncludeMembers(s => s.Inner)"));

        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }

    private static string Source(string registration)
    {
        return $$"""
#nullable enable
using Morphant;
internal struct Wrapper { public Inner Inner { get; set; } }
internal sealed class Order { public Inner Inner { get; set; } = new(); }
internal sealed class Inner { public string Name { get; set; } = ""; }
internal sealed class Dto { public string Name { get; set; } = ""; }
internal interface IDestination { }
internal sealed class Implementation : IDestination { }
internal enum Kind { One }
[MorphantMapper] internal partial class Mapper : TypeMapper
{
    protected override void Configure(MapperBuilder b) =>
        {{registration}};
}
""";
    }
}
