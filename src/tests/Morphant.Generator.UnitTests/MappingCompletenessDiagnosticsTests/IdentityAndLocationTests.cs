namespace Morphant.Generator.UnitTests.MappingCompletenessDiagnosticsTests;

[TestFixture]
internal sealed class IdentityAndLocationTests
{
    [Test]
    public void Preserves_registration_syntax_and_constructed_member_display()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using SourceAlias = TestCase.Source<string?>;

namespace TestCase
{
    public sealed class Source<T>
        where T : class?
    {
        public T? Unused { get; set; }
    }

    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SourceAlias, Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Source);
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.CompletenessDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0047"));
            Assert.That(
                MappingCompletenessDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("SourceAlias"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    MappingCompletenessDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "Unused" }));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain(
                    "TestCase.Source<string?>.Unused"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
