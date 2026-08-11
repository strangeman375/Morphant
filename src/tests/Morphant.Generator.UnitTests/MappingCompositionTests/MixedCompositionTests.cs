namespace Morphant.Generator.UnitTests.MappingCompositionTests;

[TestFixture]
internal sealed class MixedCompositionTests
{
    [Test]
    public void Primary_is_the_first_invocation_of_the_side_seen_second()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { public int Value { get; set; } }
public class Destination { public int Value { get; set; } }
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
            .Resolve((source, previous) => new())
            .Convert(source => new Destination1());

        builder.Map<Source, Destination2>()
            .Convert(source => new Destination2())
            .ConstructUsing(source => new Destination2());

        builder.Map<Source, Destination3>()
            .Members(source => new() { Value = source.Value })
            .Convert(source => new Destination3());

        builder.Map<Source, Destination4>()
            .Convert(source => new Destination4())
            .Members(source => new() { Value = source.Value });

        builder.Map<Source, Destination5>()
            .Construct(source => new())
            .Convert(source => new Destination5());

        builder.Map<Source, Destination6>()
            .Convert(source => new Destination6())
            .ResolveUsing((source, previous) => new Destination6());
    }
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0020",
                    "MORPH0020",
                    "MORPH0020",
                    "MORPH0020",
                    "MORPH0020",
                    "MORPH0020"
                }));
            Assert.That(
                PrimaryFor("Destination1"),
                Is.EqualTo("Convert"));
            Assert.That(
                PrimaryFor("Destination2"),
                Is.EqualTo("ConstructUsing"));
            Assert.That(
                PrimaryFor("Destination3"),
                Is.EqualTo("Convert"));
            Assert.That(
                PrimaryFor("Destination4"),
                Is.EqualTo("Members"));
            Assert.That(
                PrimaryFor("Destination5"),
                Is.EqualTo("Convert"));
            Assert.That(
                PrimaryFor("Destination6"),
                Is.EqualTo("ResolveUsing"));
            Assert.That(
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.AdditionalLocations.Count),
                Is.EqualTo(new[] { 2, 2, 2, 2, 2, 2 }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });

        string PrimaryFor(string destination)
        {
            var diagnostic = result.Diagnostics.Single(candidate =>
                candidate.GetMessage().Contains(
                    "global::TestCase." + destination,
                    StringComparison.Ordinal));

            return MappingCompositionGeneratorTest.SourceText(
                diagnostic.Location);
        }
    }

    [Test]
    public void Every_three_slot_order_has_one_mixed_diagnostic()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { public int Value { get; set; } }
public class Destination { public int Value { get; set; } }
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
            .Members(source => new() { Value = source.Value })
            .Convert(source => new Destination1());

        builder.Map<Source, Destination2>()
            .Construct(source => new())
            .Convert(source => new Destination2())
            .Members(source => new() { Value = source.Value });

        builder.Map<Source, Destination3>()
            .Members(source => new() { Value = source.Value })
            .Construct(source => new())
            .Convert(source => new Destination3());

        builder.Map<Source, Destination4>()
            .Members(source => new() { Value = source.Value })
            .Convert(source => new Destination4())
            .Construct(source => new());

        builder.Map<Source, Destination5>()
            .Convert(source => new Destination5())
            .Construct(source => new())
            .Members(source => new() { Value = source.Value });

        builder.Map<Source, Destination6>()
            .Convert(source => new Destination6())
            .Members(source => new() { Value = source.Value })
            .Construct(source => new());
    }
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics.Length, Is.EqualTo(6));
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Has.All.EqualTo("MORPH0020"));
            Assert.That(
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.AdditionalLocations.Count),
                Has.All.EqualTo(3));
            Assert.That(PrimaryFor("Destination1"), Is.EqualTo("Convert"));
            Assert.That(PrimaryFor("Destination2"), Is.EqualTo("Convert"));
            Assert.That(PrimaryFor("Destination3"), Is.EqualTo("Convert"));
            Assert.That(PrimaryFor("Destination4"), Is.EqualTo("Convert"));
            Assert.That(PrimaryFor("Destination5"), Is.EqualTo("Construct"));
            Assert.That(PrimaryFor("Destination6"), Is.EqualTo("Members"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });

        string PrimaryFor(string destination)
        {
            var diagnostic = result.Diagnostics.Single(candidate =>
                candidate.GetMessage().Contains(
                    "global::TestCase." + destination,
                    StringComparison.Ordinal));

            return MappingCompositionGeneratorTest.SourceText(
                diagnostic.Location);
        }
    }

    [Test]
    public void All_three_slots_have_fixed_additional_location_order()
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
            .Members(source => new() { Value = source.Value })
            .Construct(source => new())
            .Convert(source => new Destination());
    }
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0020"));
            Assert.That(
                MappingCompositionGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Convert"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(location =>
                    MappingCompositionGeneratorTest.SourceText(location)),
                Is.EqualTo(new[] { "Construct", "Members", "Convert" }));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Convert cannot be combined with a result policy or " +
                    "Members for contract " +
                    "'global::Morphant.ITypeMapper<global::TestCase.Source, " +
                    "global::TestCase.Destination>' in mapper " +
                    "'global::TestCase.TestMapper'."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Duplicate_and_mixed_conflicts_are_independent()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>()
            .Convert(source => new Destination())
            .Construct(source => new())
            .Convert(source => new Destination());
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0019", "MORPH0020" }));
            Assert.That(
                result.Diagnostics[0].AdditionalLocations,
                Has.Count.EqualTo(1));
            Assert.That(
                result.Diagnostics[1].AdditionalLocations,
                Has.Count.EqualTo(2));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
