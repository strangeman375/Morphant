# MORPH0022: Invalid MSBuild mapping setting value

## Cause

A `Morphant...` MSBuild property contains a value that is not declared by its
corresponding setting enum. The diagnostic names the property and rejected
value.

## Fix

Replace it with one of the documented names. Values are case-insensitive;
missing, empty, and `Default` values continue to the next configuration level.

```xml
<PropertyGroup>
  <MorphantMemberSelection>Explicit</MorphantMemberSelection>
</PropertyGroup>
```

See [Settings](../settings/README.md) for every property and supported value.

[All diagnostics](../diagnostics.md)
