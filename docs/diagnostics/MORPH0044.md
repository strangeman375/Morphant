# MORPH0044: Nested mapping types cannot be determined

## Cause

Morphant cannot determine the exact source/destination pair for `Map`, `Create`,
or `Update`. A parameterless call may have no matching source member, a source
expression such as plain `null` may have no usable static type, or the target
may not reveal the nested destination type.

## Fix

Supply a typed source expression and, when needed, the destination type:

```csharp
BillingAddress = Map<Address>(source.InvoiceAddress)
```

Cast `null` or `default` to the intended source type instead of leaving it
untyped.

See [Nested mapping](../nested-mapping.md).

[All diagnostics](../diagnostics.md)
