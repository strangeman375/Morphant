namespace Morphant.Generator.UnitTests.MappingCompositionTests;

[TestFixture]
internal sealed class DuplicateSlotTests
{
    [Test]
    public void Every_occurrence_after_the_first_reports_the_first_slot()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { public int Value { get; set; } }
public sealed class Destination { public int Value { get; set; } }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, Destination>()
            .Construct(source => new())
            .ResolveUsing((source, previous) => new Destination())
            .ConstructUsing(source => new Destination());
    }
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);
        var diagnostics = result.Diagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0019", "MORPH0019" }));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    MappingCompositionGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "ResolveUsing", "ConstructUsing" }));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    MappingCompositionGeneratorTest.SourceText(
                        diagnostic.AdditionalLocations.Single())),
                Is.EqualTo(new[] { "Construct", "Construct" }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.All.EqualTo(
                    "'Construct or Resolve' is configured more than once " +
                    "for mapping 'TestCase.Source -> TestCase.Destination' " +
                    "in mapper " +
                    "'TestCase.TestMapper'."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Members_and_Convert_each_report_second_and_third_occurrences()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { public int Value { get; set; } }
public sealed class MembersDestination { public int Value { get; set; } }
public sealed class ConvertDestination { public int Value { get; set; } }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, MembersDestination>()
            .Members(source => new() { Value = source.Value })
            .Members(source => new() { Value = source.Value + 1 })
            .Members(source => new() { Value = source.Value + 2 });

        builder.Map<Source, ConvertDestination>()
            .Convert(source => new ConvertDestination())
            .Convert((source, previous) => new ConvertDestination())
            .Convert((source, previous, context) =>
                new ConvertDestination());
    }
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);
        var diagnostics = result.Diagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics.Length, Is.EqualTo(4));
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Has.All.EqualTo("MORPH0019"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.AdditionalLocations.Count),
                Has.All.EqualTo(1));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.Location.GetLineSpan()
                        .StartLinePosition.Line + 1),
                Is.EqualTo(new[] { 25, 26, 20, 21 }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.AdditionalLocations[0].GetLineSpan()
                        .StartLinePosition.Line + 1),
                Is.EqualTo(new[] { 24, 24, 19, 19 }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Every_pair_of_different_result_policy_names_is_one_slot()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { }
public class Destination { }
public sealed class Destination1 : Destination { }
public sealed class Destination2 : Destination { }
public sealed class Destination3 : Destination { }
public sealed class Destination4 : Destination { }
public sealed class Destination5 : Destination { }
public sealed class Destination6 : Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, Destination1>()
            .Construct(source => new())
            .Resolve((source, previous) => new());
        builder.Map<Source, Destination2>()
            .Construct(source => new())
            .ConstructUsing(source => new Destination2());
        builder.Map<Source, Destination3>()
            .Construct(source => new())
            .ResolveUsing((source, previous) => new Destination3());
        builder.Map<Source, Destination4>()
            .Resolve((source, previous) => new())
            .ConstructUsing(source => new Destination4());
        builder.Map<Source, Destination5>()
            .Resolve((source, previous) => new())
            .ResolveUsing((source, previous) => new Destination5());
        builder.Map<Source, Destination6>()
            .ConstructUsing(source => new Destination6())
            .ResolveUsing((source, previous) => new Destination6());
    }
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics.Length, Is.EqualTo(6));
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Has.All.EqualTo("MORPH0019"));
            Assert.That(
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.AdditionalLocations.Count),
                Has.All.EqualTo(1));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
