#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace ConfigurationMissing;

[MorphantMapper]
public abstract partial class MissingMapper : TypeMapper<MissingMapper>
{
}

public abstract class ConcreteBaseMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : ConcreteBaseMapper<TMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
    }
}

[MorphantMapper]
public abstract partial class InheritedMapper :
    ConcreteBaseMapper<InheritedMapper>
{
}
