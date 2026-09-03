using System;
using Morphant;
using Morphant.Context;

namespace Morphant.DeclarationValidForms;

public sealed class Source
{
}

public sealed class DestinationA
{
}

public sealed class DestinationB
{
}

public sealed class DestinationC
{
}

public sealed class DestinationD
{
}

public sealed class InheritedDestination
{
}

public sealed class SupportsDestination
{
}

public sealed class Box<T>
{
}

[MorphantMapper]
public abstract partial class AbstractMapper : TypeMapper<AbstractMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, DestinationA>();
}

[MorphantMapper]
public partial class NonSealedMapper : TypeMapper<NonSealedMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, DestinationB>();
}

[MorphantMapper]
public partial class ClosedGenericMapper<T> : TypeMapper<ClosedGenericMapper<T>>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Box<int>, Box<string>>();
}

public partial class MapperContainer
{
    [MorphantMapper]
    protected partial class ProtectedMapper : TypeMapper<ProtectedMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, DestinationC>();
    }

    [MorphantMapper]
    private partial class PrivateMapper : TypeMapper<PrivateMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, DestinationD>();
    }
}

public abstract class ContractBaseMapper<TMapper> :
    TypeMapper<TMapper>,
    ITypeMapper<Source, InheritedDestination>
    where TMapper : ContractBaseMapper<TMapper>
{
    public InheritedDestination Create(
        Source? source,
        MappingContext context) =>
        throw new NotSupportedException();

    public InheritedDestination Update(
        Source? source,
        InheritedDestination? destination,
        MappingContext context) =>
        throw new NotSupportedException();
}

[MorphantMapper]
public partial class DerivedContractMapper :
    ContractBaseMapper<DerivedContractMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, InheritedDestination>();
}

public abstract class SupportsBaseMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : SupportsBaseMapper<TMapper>
{
    protected override bool Supports(
        Type sourceType,
        Type destinationType) =>
        false;
}

[MorphantMapper]
public partial class DerivedSupportsMapper :
    SupportsBaseMapper<DerivedSupportsMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, SupportsDestination>();
}
