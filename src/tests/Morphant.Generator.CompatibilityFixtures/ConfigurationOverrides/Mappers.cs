#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Generator.UnitTests.TestAssets.ConfigurationOverridesScenario;

namespace ConfigurationOverrides;

public sealed class SourceA { }
public sealed class DestinationA { }
public sealed class SourceB { }
public sealed class DestinationB { }
public sealed class SourceC { }
public sealed class DestinationC { }

[MorphantMapper]
public abstract partial class MissingMapper : TypeMapper<MissingMapper>
{
}

[MorphantMapper]
public partial class UnavailableBaseMapper :
    MetadataConfigurationBase<UnavailableBaseMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        builder.Map<SourceA, DestinationA>();
    }
}

[MorphantMapper]
public partial class RootFlowMapper : TypeMapper<RootFlowMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        Observe(builder);
        builder.Map<SourceB, DestinationB>();
    }

    private static void Observe(MapperBuilder builder)
    {
    }
}

[MorphantMapper]
public partial class PairFlowMapper : TypeMapper<PairFlowMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        var mapping = builder.Map<SourceC, DestinationC>();
        _ = mapping;
    }
}
