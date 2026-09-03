# MORPH0058: Mapper self type is invalid

## Cause

An attributed mapper closes `TypeMapper<TMapper>` with a type that does not
represent that mapper's configuration family.

For a concrete mapper, `TMapper` must be the mapper itself. A reusable generic
base may use a CRTP self type only when that type parameter is constrained back
to the base.

```csharp
[MorphantMapper]
public partial class OrderMapper : TypeMapper<CustomerMapper>
{
}
```

Using an unrelated self type would make generated fluent methods belong to the
wrong mapper scope, so Morphant does not generate the mapper.

## Fix

Close `TypeMapper<TMapper>` with the concrete mapper type:

```csharp
[MorphantMapper]
public partial class OrderMapper : TypeMapper<OrderMapper>
{
}
```

For reusable configuration, use a correctly constrained CRTP base:

```csharp
public abstract partial class CommonMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : CommonMapper<TMapper>
{
}

[MorphantMapper]
public partial class OrderMapper : CommonMapper<OrderMapper>
{
}
```

[All diagnostics](../diagnostics.md)
