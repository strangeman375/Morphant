# MORPH0015: Mapper must declare Configure

## Cause

The mapper has no readable override of `Configure(MapperBuilder)`. An inherited
implementation alone does not define the mappings generated for the current
mapper.

## Fix

Declare the override in the mapper and put its configuration in source code:

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.Map<Source, Destination>();
}
```

Call `base.Configure(builder)` inside it when base configuration should be
included.

[All diagnostics](../diagnostics.md)
