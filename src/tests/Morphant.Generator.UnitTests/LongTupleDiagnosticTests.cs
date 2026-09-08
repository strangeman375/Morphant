using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class LongTupleDiagnosticTests
{
    private static IEnumerable<TestCaseData> Shapes =>
    [
        new("System.Tuple<int, int, int, int, int, int, int, System.Tuple<int>>", 8),
        new("System.Tuple<int, int, int, int, int, int, int, System.Tuple<int, int, int, int, int, int, int, System.Tuple<int>>>", 15)
    ];

    private static IEnumerable<TestCaseData> SourceShapes => Shapes.Concat(
    [
        new("System.ValueTuple<int, int, int, int, int, int, int, System.ValueTuple<int>>", 8),
        new("System.ValueTuple<int, int, int, int, int, int, int, System.ValueTuple<int, int, int, int, int, int, int, System.ValueTuple<int>>>", 15)
    ]);

    [TestCaseSource(nameof(Shapes))]
    public void Readonly_rule_diagnostic_names_the_logical_tail_element(string type, int ordinal)
    {
        // lang=c#
        const string template =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
using Destination = __TYPE__;
namespace TestCase
{
    public sealed class Source { public int Value { get; set; } }
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        private static Destination CreateDestination() => throw new System.NotImplementedException();
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructUsing(source => CreateDestination())
                .Members(source => new() { __MEMBER__ = source.Value });
    }
}
""";
        var member = "Item" + ordinal;
        var source = template.Replace("__TYPE__", type).Replace("__MEMBER__", member);
        var result = GeneratorTestDriver.Run("LongTupleDiagnosticConsumer", source, LanguageVersion.CSharp9);
        Assert.Multiple(() =>
        {
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.EffectiveDiagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0042" }));
        });
        var diagnostic = result.EffectiveDiagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Location.SourceSpan,
                Is.EqualTo(new TextSpan(source.IndexOf(member + " =", StringComparison.Ordinal), member.Length)));
            Assert.That(diagnostic.AdditionalLocations.Select(GeneratorTestDriver.GetSourceText),
                Is.EqualTo(new[] { "ConstructUsing" }));
            Assert.That(diagnostic.GetMessage(), Is.EqualTo(
                $"Rule for destination member 'element #{ordinal} ({member})' cannot be applied in mapping 'TestCase.Source -> {type}': " +
                "read-only tuple element cannot be assigned after ConstructUsing or ResolveUsing returns. " +
                "Affected cases: Create; Update without an existing destination."));
        });
    }

    [TestCaseSource(nameof(SourceShapes))]
    public void Completeness_diagnostic_names_only_the_unused_tail_element(string type, int ordinal)
    {
        // lang=c#
        const string template =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
using SourceTuple = __TYPE__;
namespace TestCase
{
    public sealed class Destination { public int Value { get; set; } }
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SourceTuple, Destination>()
                .Members(source => new()
                {
                    Value = source.Item1 + source.Item2 + source.Item3 + source.Item4 + source.Item5 + source.Item6 + source.Item7__REST__
                })
                .UnmappedMemberValidation(UnmappedMemberValidation.Source);
    }
}
""";
        var source = template.Replace("__TYPE__", type).Replace("__REST__", ordinal == 15
            ? " + source.Rest.Item1 + source.Rest.Item2 + source.Rest.Item3 + source.Rest.Item4 + source.Rest.Item5 + source.Rest.Item6 + source.Rest.Item7"
            : "");
        var result = GeneratorTestDriver.Run("LongTupleCompletenessConsumer", source, LanguageVersion.CSharp9);
        Assert.Multiple(() =>
        {
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.EffectiveDiagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0047" }),
                string.Join(Environment.NewLine, result.EffectiveDiagnostics));
        });
        var diagnostic = result.EffectiveDiagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Location.SourceSpan,
                Is.EqualTo(new TextSpan(source.IndexOf("Map<SourceTuple", StringComparison.Ordinal) + 4, "SourceTuple".Length)));
            Assert.That(diagnostic.GetMessage(), Is.EqualTo(
                $"Source member 'element #{ordinal} (Item{ordinal})' is not used by mapping '{type} -> TestCase.Destination'."));
        });
    }

    [TestCaseSource(nameof(SourceShapes))]
    public void Passing_Rest_to_a_helper_accounts_for_the_whole_tail(string type, int ordinal)
    {
        // lang=c#
        const string template =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
using SourceTuple = __TYPE__;
namespace TestCase
{
    public sealed class Destination { public int Value { get; set; } }
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        private static int Read(object value) => value.GetHashCode();
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SourceTuple, Destination>()
                .Members(source => new()
                {
                    Value = source.Item1 + source.Item2 + source.Item3 + source.Item4 + source.Item5 + source.Item6 + source.Item7 + Read(source.Rest)
                })
                .UnmappedMemberValidation(UnmappedMemberValidation.Source);
    }
}
""";
        var result = GeneratorTestDriver.Run("LongTupleTailConsumer",
            template.Replace("__TYPE__", type), LanguageVersion.CSharp9);
        Assert.Multiple(() =>
        {
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
        });
    }
}
