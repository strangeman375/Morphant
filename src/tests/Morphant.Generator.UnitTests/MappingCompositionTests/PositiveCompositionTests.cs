namespace Morphant.Generator.UnitTests.MappingCompositionTests;

[TestFixture]
internal sealed class PositiveCompositionTests
{
    [Test]
    public void Every_supported_local_slot_set_has_no_composition_diagnostic()
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
public sealed class EmptyDestination : Destination { }
public sealed class ConstructDestination : Destination { }
public sealed class ResolveDestination : Destination { }
public sealed class ConstructUsingDestination : Destination { }
public sealed class ResolveUsingDestination : Destination { }
public sealed class MembersDestination : Destination { }
public sealed class ConvertDestination : Destination { }
public sealed class ConstructMembersDestination : Destination { }
public sealed class MembersResolveDestination : Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, EmptyDestination>();

        builder.Map<Source, ConstructDestination>()
            .Construct(source => new());

        builder.Map<Source, ResolveDestination>()
            .Resolve((source, previous) => new());

        builder.Map<Source, ConstructUsingDestination>()
            .ConstructUsing(source => new ConstructUsingDestination());

        builder.Map<Source, ResolveUsingDestination>()
            .ResolveUsing((source, previous) =>
                new ResolveUsingDestination());

        builder.Map<Source, MembersDestination>()
            .Members(source => new() { Value = source.Value });

        builder.Map<Source, ConvertDestination>()
            .Convert(source =>
                new ConvertDestination { Value = source!.Value });

        builder.Map<Source, ConstructMembersDestination>()
            .Construct(source => new())
            .Members(source => new() { Value = source.Value });

        builder.Map<Source, MembersResolveDestination>()
            .Members(source => new() { Value = source.Value })
            .Resolve((source, previous) => new());
    }
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
