# MORPH0062: Structured construction result is invalid

## Cause

`Construct` or `Resolve` returns a value outside its supported result forms.
The implicit conversion from the destination type enables reuse syntax but
does not allow arbitrary destination objects in these callbacks.

## Fix

In `Construct`, return a generated construction expression such as
`new(source.Id)` or `new OrderConstruction(source.Id)`.

In `Resolve`, you may also return `previous.Value` after checking `HasValue`,
the variable from a successful `previous.TryGetValue`, or an unchanged local
alias of either value. Use an explicit construction type for the other branch
of a mixed conditional expression, so C# does not infer an ordinary destination.

Use [`ConstructUsing`](../api/construct-using.md) or
[`ResolveUsing`](../api/resolve-using.md) for factories, cached objects,
`new Order(...)`, or helper calls returning a destination. Morphant does not
inspect helper implementations to prove that they return the existing object.

See [`Resolve`](../api/resolve.md) for a reuse example.

[All diagnostics](../diagnostics.md)
