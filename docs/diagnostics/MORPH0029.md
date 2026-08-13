# MORPH0029: Mapping expression must be an inline lambda

## Cause

`Construct`, `Resolve`, or `Members` receives a method group, delegate variable,
or another value instead of an inline lambda. Morphant needs the lambda body to
generate the mapping.

## Fix

Write the expression inline:

```csharp
.Construct(source => new(source.Id))
```

When the logic must remain an ordinary runtime callback, use
`ConstructUsing`, `ResolveUsing`, or `Convert` as appropriate.

See [Declarative mapping](../declarative-mapping.md).

[All diagnostics](../diagnostics.md)
