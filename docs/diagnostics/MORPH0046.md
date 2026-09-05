# MORPH0046: Nested Update destination is invalid

## Cause

An explicit nested `Update` has no destination value that can be used in every
affected operation, or the supplied value cannot contain the exact nested
destination type. The standalone form for a read-only member must refer to that
member through the generated `Members` callback result type.

## Fix

Pass a compatible current destination to `Update`. Use adaptive `Map` when a
current value is not always available, or use the documented standalone
read-only member form when the member has no setter. A writable member is
required when a replacement result must be assigned back.

For a standalone call, use `Update(source.Child, members.Child)`, where
`members` is the generated object returned from `Members`. Access through the
callback's `result.Child` or an unrelated object is not supported. See the
[read-only member example](../nested-mapping.md#read-only-members).

[All diagnostics](../diagnostics.md)
