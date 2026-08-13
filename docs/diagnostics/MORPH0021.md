# MORPH0021: Invalid mapping setting value

## Cause

A C# setting argument is not a supported compile-time constant. Local
variables, runtime expressions, and numeric values outside the declared enum
values cannot define generated behavior.

## Fix

Pass an enum member or a valid `const` expression directly:

```csharp
builder.Map<Source, Destination>()
    .MemberSelection(MemberSelection.Explicit);
```

If several calls set the same value at one level, only the last effective call
matters.

See [Settings](../settings/README.md).

[All diagnostics](../diagnostics.md)
