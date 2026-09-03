using Morphant;
using Morphant.Context;

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
public class NonPartialMapper : TypeMapper<NonPartialMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, NonPartialDestination>();
}

public class NonPartialContainer
{
    [MorphantMapper]
    public partial class NestedMapper : TypeMapper<NestedMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, NestedDestination>();
    }
}

[MorphantMapper]
file partial class FileMapper : TypeMapper<FileMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, FileDestination>();
}

[MorphantMapper]
public abstract partial class UnifiableContractMapper<T> :
    TypeMapper<UnifiableContractMapper<T>>,
    ITypeMapper<T, NestedDestination>
{
    public abstract NestedDestination Create(
        T? source,
        MappingContext context);

    public abstract NestedDestination Update(
        T? source,
        NestedDestination? destination,
        MappingContext context);

    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, NestedDestination>();
}
