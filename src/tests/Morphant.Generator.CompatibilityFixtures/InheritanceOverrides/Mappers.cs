#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace InheritanceOverrides;

public sealed class Source { public string Value { get; init; } = ""; }
public sealed class Destination { public string Value { get; set; } = ""; }
public class BaseSource { }
public sealed class DerivedSource : BaseSource { }
public class BaseDestination { }
public sealed class DerivedDestination : BaseDestination { }
public sealed class MissingSource { }
public sealed class MissingDestination { }
public sealed class UnrelatedSource { }

public abstract class DuplicateConfigureBase<TMapper> : TypeMapper<TMapper>
    where TMapper : DuplicateConfigureBase<TMapper>
{
    protected override void Configure(MapperBuilder builder) { }
}

[MorphantMapper]
public partial class DuplicateConfigureMapper :
    DuplicateConfigureBase<DuplicateConfigureMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        base.Configure(builder);
    }
}

[MorphantMapper]
public partial class DuplicateIncludeMapper : TypeMapper<DuplicateIncludeMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<BaseSource, BaseDestination>();
        builder.Map<DerivedSource, DerivedDestination>()
            .IncludeBase<BaseSource, BaseDestination>()
            .IncludeBase<BaseSource, BaseDestination>();
    }
}

[MorphantMapper]
public partial class MissingIncludeMapper : TypeMapper<MissingIncludeMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>()
            .IncludeBase<MissingSource, MissingDestination>();
}

[MorphantMapper]
public partial class IncompatibleIncludeMapper : TypeMapper<IncompatibleIncludeMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<BaseSource, BaseDestination>();
        builder.Map<UnrelatedSource, DerivedDestination>()
            .IncludeBase<BaseSource, BaseDestination>();
    }
}

public abstract class InaccessibleCallbackBase<TMapper> : TypeMapper<TMapper>
    where TMapper : InaccessibleCallbackBase<TMapper>
{
    private static string Secret(string value) => value;

    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>()
            .Members(source => new()
            {
                Value = Secret(source.Value)
            });
}

[MorphantMapper]
public partial class InaccessibleCallbackMapper :
    InaccessibleCallbackBase<InaccessibleCallbackMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        builder.Map<Source, Destination>()
            .IncludeBase<Source, Destination>();
    }
}
