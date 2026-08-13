# MORPH0047: Source member is not used

## Cause

`UnmappedMemberValidation` checks source members, and the reported readable
source property or field does not participate in the final declarative mapping.
This warning does not change generated behavior.

## Fix

Use the member in a constructor, member, or nested-mapping rule. If it is
intentionally unused, acknowledge it without reading it at runtime:

```csharp
.Members((source, _) =>
{
    _ = source.LegacyValue;
    return new() { Name = source.Name };
})
```

You can also relax `UnmappedMemberValidation` or configure the warning severity.
See [Unmapped member validation](../settings/unmapped-member-validation.md).

[All diagnostics](../diagnostics.md)
