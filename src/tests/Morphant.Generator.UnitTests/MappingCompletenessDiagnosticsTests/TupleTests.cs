namespace Morphant.Generator.UnitTests.MappingCompletenessDiagnosticsTests;

[TestFixture]
internal sealed class TupleTests
{
    [Test]
    public void Treats_an_alias_and_its_underlying_Item_name_as_one_source_element()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Destination
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int Id, string Name), Destination>()
                .Members(source => new()
                {
                    Id = source.Item1,
                    Name = source.Item2
                })
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict);
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.CompletenessDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Does_not_count_an_overridden_construction_rule_as_source_usage()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed record Source(int Discarded, string Kept, int Replacement);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, (int, string)>()
                .Construct(source => new(source.Discarded, source.Kept))
                .Members(source => new()
                {
                    Item1 = source.Replacement
                })
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
                Is.EqualTo("Source"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    MappingCompletenessDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "int Discarded" }));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Source member 'TestCase.Source.Discarded' is not used " +
                    "by mapping 'TestCase.Source -> " +
                    "System.ValueTuple<int, string>'."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_unnamed_tuple_elements_instead_of_matching_Item_names()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Destination
    {
        public int Item1 { get; set; }

        public string Item2 { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int, string), Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict);
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.That(
            result.CompletenessDiagnostics.Length,
            Is.EqualTo(4),
            string.Join(
                Environment.NewLine,
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));

        Assert.Multiple(() =>
        {
            Assert.That(
                result.CompletenessDiagnostics.Select(
                    static diagnostic => diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0047",
                    "MORPH0047",
                    "MORPH0048",
                    "MORPH0048"
                }));
            Assert.That(
                result.CompletenessDiagnostics.Select(
                    static diagnostic => diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "Source member 'element #1 (Item1)' is not used by " +
                    "mapping 'System.ValueTuple<int, string> -> " +
                    "TestCase.Destination'.",
                    "Source member 'element #2 (Item2)' is not used by " +
                    "mapping 'System.ValueTuple<int, string> -> " +
                    "TestCase.Destination'.",
                    "Destination member 'TestCase.Destination.Item1' is not " +
                    "mapped by mapping 'System.ValueTuple<int, string> -> " +
                    "TestCase.Destination'.",
                    "Destination member 'TestCase.Destination.Item2' is not " +
                    "mapped by mapping 'System.ValueTuple<int, string> -> " +
                    "TestCase.Destination'."
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
