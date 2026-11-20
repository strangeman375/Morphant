#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace SettingsOverrides;

public sealed class PropertySourceA { }
public sealed class PropertyDestinationA { }
public sealed class PropertySourceB { }
public sealed class PropertyDestinationB { }
public sealed class OverriddenSource { }
public sealed class OverriddenDestination { }
public sealed class CSharpSource { }
public sealed class CSharpDestination { }
public sealed class ManualSource { }
public sealed class ManualDestination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        var invalid = MemberSelection.Auto;

        builder.Map<PropertySourceA, PropertyDestinationA>();
        builder.Map<PropertySourceB, PropertyDestinationB>();
        builder.Map<OverriddenSource, OverriddenDestination>()
            .MemberSelection(MemberSelection.Explicit);
        builder.Map<CSharpSource, CSharpDestination>()
            .MemberSelection(invalid);
        builder.Map<ManualSource, ManualDestination>()
            .NullSourceHandling(NullSourceHandling.Throw)
            .Convert(source => new ManualDestination());
    }
}
