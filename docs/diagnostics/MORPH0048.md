# MORPH0048: Destination member is not mapped

## Cause

`UnmappedMemberValidation` checks destination members, and the reported
assignable property or field is not occupied by the final declarative mapping.
This warning does not change generated behavior.

## Fix

Map the member by convention, `Auto()`, an explicit value, or a nested mapping.
Use `Ignore()` when leaving it unchanged is intentional. You can also relax
`UnmappedMemberValidation` or configure the warning severity.

See [Unmapped member validation](../settings/unmapped-member-validation.md).

[All diagnostics](../diagnostics.md)
