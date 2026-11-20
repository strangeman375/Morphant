#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace MappingCompletenessOverrides;

public sealed class Source
{
    public int Used { get; set; }

    public int Unused { get; set; }
}

public sealed class Destination
{
    public int Used { get; set; }

    public int Unmapped { get; set; }
}

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>()
            .Members(source => new() { Used = Value(source.Used) });
}
