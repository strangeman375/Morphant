# MORPH0040: Member rule is invalid

## Cause

An explicit destination-member rule cannot be applied. Common reasons are an
`Auto()` rule without exactly one compatible source member, a `Value<T>` whose
type differs from the destination member type, or an inherited rule hidden by
a new member on the current destination type.

## Fix

Follow the reason in the diagnostic message. Use a compatible explicit value,
correct the exact type passed to `Value<T>`, or replace an invalid inherited
rule with a local rule for the current destination member.

See [Declarative mapping](../declarative-mapping.md).

[All diagnostics](../diagnostics.md)
