#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace RegistrationUnavailable;

file sealed class HiddenSource { }

public sealed class IndependentSource { }

public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<HiddenSource, Destination>();
        builder.Map<IndependentSource, Destination>()
            .Convert(source => new Destination());
    }
}
