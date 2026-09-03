#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace ConstructionOverrides;

public sealed class Source { }

public interface IMissingDestination { }

public sealed class ConventionDestination
{
    public ConventionDestination() { }
}

public sealed class RuleDestination
{
    public RuleDestination(int value) { }
}

public sealed class PreviousDestination { }

public sealed class NullPlanDestination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, IMissingDestination>();
        builder.Map<Source, ConventionDestination>()
            .ConstructorSelection(ConstructorSelection.Explicit);
        builder.Map<Source, RuleDestination>()
            .Construct(source => new(Auto()));
        builder.Map<Source, PreviousDestination>()
            .Resolve((source, previous) => previous);
        builder.Map<Source, NullPlanDestination>()
            .Construct(source => default!);
    }
}
