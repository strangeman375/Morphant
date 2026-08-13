# MORPH0046: Nested Update destination is invalid

## Cause

An explicit nested `Update` has no destination value that can be used in every
affected operation, or the supplied value cannot contain the exact nested
destination type. The standalone form for a read-only member must refer to that
member's current value.

## Fix

Pass a compatible current destination to `Update`. Use adaptive `Map` when a
current value is not always available, or use the documented standalone
read-only member form when the member has no setter. A writable member is
required when a replacement result must be assigned back.

See [Nested mapping](../nested-mapping.md).

[All diagnostics](../diagnostics.md)
