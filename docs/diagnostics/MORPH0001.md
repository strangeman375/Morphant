# MORPH0001: Unsupported C# language version

## Cause

Morphant requires C# 9 or later, but the project is compiled with an older
effective language version.

## Fix

Use C# 9 or newer. Remove an older `LangVersion` override or update it in the
project file:

```xml
<PropertyGroup>
  <LangVersion>9.0</LangVersion>
</PropertyGroup>
```

[All diagnostics](../diagnostics.md)
