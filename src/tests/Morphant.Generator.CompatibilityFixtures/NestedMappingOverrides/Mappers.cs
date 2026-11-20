#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace NestedMappingOverrides;

public sealed class ChildSource { }

public sealed class ChildDestination { }

public sealed class Source
{
    public ChildSource Child { get; } = new();

    public int Value { get; set; }
}

public sealed class UnknownDestination
{
    public ChildDestination? Child { get; set; }
}

public sealed class IncompatibleDestination
{
    public string Text { get; set; } = string.Empty;
}

public sealed class UpdateDestination
{
    public ChildDestination? Child { get; set; }
}

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, UnknownDestination>()
            .Members(source => new() { Child = Map(null) });
        builder.Map<Source, IncompatibleDestination>()
            .Members(source => new()
            {
                Text = Map<int>(source.Value)
            });
        builder.Map<Source, UpdateDestination>()
            .Members(source => new()
            {
                Child = Update<ChildDestination>(
                    source.Child,
                    new object())
            });
    }
}
