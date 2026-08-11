using Morphant;

namespace Morphant.DeclarationStructuralFailures;

public sealed class Source
{
}

public sealed class NonPartialDestination
{
}

public sealed class NestedDestination
{
}

public sealed class FileDestination
{
}

[MorphantMapper]
public class NonPartialMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, NonPartialDestination>();
}

public class NonPartialContainer
{
    [MorphantMapper]
    public partial class NestedMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, NestedDestination>();
    }
}

[MorphantMapper]
file partial class FileMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, FileDestination>();
}
