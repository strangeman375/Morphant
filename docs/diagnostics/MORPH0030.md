# MORPH0030: Mapping expression is unavailable

## Cause

A mapping callback references code that will not be available from the
generated mapper. This applies to `Construct`, `Resolve`, `Members`,
`ConstructUsing`, `ResolveUsing`, and `Convert`. It can also occur when an
expression violates the input or nullable contract where the mapping uses it,
including a `Members` value passed to a constructor.

Ordinary obsolete-use warnings from constructors or members selected by
conventions remain compiler diagnostics. See
[compiler warnings](../api/README.md#compiler-warnings) for warning ownership
and local suppression.

### Collection initializers

Collection initializers can also produce this diagnostic when preserving the
original caller-information values and `Add` overload requires explicit method
calls. Two combinations are unsupported:

- an `init` assignment after a property collection initializer, such as
  `new Holder { Items = { value }, After = value }` when `After` is init-only;
- `await` inside a property collection initializer nested in an expression
  where preserving execution would require moving the await to another
  function, for example the creation branch of a conditional expression.

These cases report `MORPH0030` with `CS8852` or `CS4032`, respectively. Ordinary
initializers that can retain their original `Add` binding are unaffected.

## Fix

Use constants or accessible mapper or static members. Do not capture
`Configure` locals or local functions, and do not reference inaccessible or
file-local symbols. The end of the diagnostic identifies the unavailable
reference or incompatible expression. For nullable values, provide a value
that satisfies the receiving parameter or member, for example with `??`.

For an unsupported collection initializer, move the complete object creation
into an ordinary accessible mapper or static method and call it from the
callback. Move an enclosing async operation as a whole so its context-dependent
work stays together. An ordinary method is supported; a local function declared
in `Configure` is not available to the generated mapper.

See [Callback forms](../api/README.md#callback-forms).

[All diagnostics](../diagnostics.md)
