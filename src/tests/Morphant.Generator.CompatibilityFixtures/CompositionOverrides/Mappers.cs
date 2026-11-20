#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace CompositionOverrides;

public sealed class Source { public int Value { get; set; } }
public sealed class DuplicateDestination { public int Value { get; set; } }
public sealed class MixedDestination { public int Value { get; set; } }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, DuplicateDestination>()
            .Construct(source => new())
            .Resolve((source, previous) => new());

        builder.Map<Source, MixedDestination>()
            .Convert(source => new MixedDestination())
            .Members(source => new() { Value = source.Value });
    }
}
