#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace MemberOverrides;

public sealed class Source
{
    public int Value { get; init; }
}

public sealed class InvalidRuleDestination
{
    public int Missing { get; set; }
}

public sealed class RequiredDestination
{
    public required int Value { get; init; }
}

public sealed class LifecycleDestination
{
    public int Value { get; init; }
}

public sealed class NullPlanDestination
{
    public int Value { get; set; }
}

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, InvalidRuleDestination>()
            .Members(source => new() { Missing = Auto() });
        builder.Map<Source, RequiredDestination>()
            .MemberSelection(MemberSelection.Explicit);
        builder.Map<Source, LifecycleDestination>()
            .ConstructUsing(source => new LifecycleDestination())
            .Members(source => new() { Value = source.Value });
        builder.Map<Source, NullPlanDestination>()
            .Members(source => default!);
    }
}
