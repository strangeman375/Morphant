# MORPH0045: Nested mapping result is incompatible

## Cause

The result type of a nested mapping cannot be assigned to the constructor
parameter or destination member that receives it. Morphant accepts only an
implicit C# conversion that introduces no compiler warning.

## Fix

Select the correct nested destination type, change the receiving member type,
or map to an intermediate value with a compatible conversion. Also check
nullable annotations when the types otherwise appear identical.

See [Nested mapping](../nested-mapping.md).

[All diagnostics](../diagnostics.md)
