# MORPH0058: Mapper self type is invalid

## Cause

A mapper or reusable configuration base closes `TypeMapper<TMapper>` with a
type that does not represent that mapper's configuration family.

For a concrete mapper, `TMapper` must be the mapper itself. A reusable generic
base may use a self type only when that type parameter is constrained back
to the base.

```csharp
[MorphantMapper]
public partial class OrderMapper : TypeMapper<CustomerMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
```

## Fix

Close `TypeMapper<TMapper>` with the concrete mapper type:

```csharp
[MorphantMapper]
public partial class OrderMapper : TypeMapper<OrderMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
```

For reusable configuration, use a base constrained to its final mapper type:

```csharp
public abstract class CommonMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : CommonMapper<TMapper>
{
}

[MorphantMapper]
public partial class OrderMapper : CommonMapper<OrderMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
```

[All diagnostics](../diagnostics.md)
