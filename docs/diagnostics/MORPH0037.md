# MORPH0037: Constructor parameter rule is invalid

## Cause

An effective constructor-parameter rule cannot be applied. Examples include an
`Auto()` rule without exactly one compatible source member, `Ignore()` on a
required parameter, a `Value<T>` type that differs from the parameter type, or
a `ByConvention()` override for a parameter absent from the selected
constructor.

## Fix

Follow the reason in the diagnostic message. Supply a compatible expression,
omit only optional or `params` parameters, make `Value<T>` use the exact
parameter type, or update the named overrides to match the selected
constructor. If [`Members`](../api/members.md#constructor-parameters) supplies
the argument, check that value against the constructor parameter's input contract.

See [Constructor selection](../settings/constructor-selection.md).

[All diagnostics](../diagnostics.md)
