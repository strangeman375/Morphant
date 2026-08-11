#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace ConfigurationMissing;

[MorphantMapper]
public abstract partial class MissingMapper : TypeMapper
{
}

public abstract class ConcreteBaseMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
    }
}

[MorphantMapper]
public abstract partial class InheritedMapper : ConcreteBaseMapper
{
}
