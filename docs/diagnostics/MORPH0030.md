# MORPH0030: Mapping expression is unavailable

## Cause

A mapping callback references code that will not be available from the
generated mapper. This applies to `Construct`, `Resolve`, `Members`,
`ConstructUsing`, `ResolveUsing`, and `Convert`. It can also occur when an
expression violates the input or nullable contract where the mapping uses it,
including a `Members` value passed to a constructor.

### Collection initializers

Some collection initializers whose `Add` method uses caller-information
parameters are unsupported in callbacks. A later `init` assignment in the
same object initializer can report `CS8852`; an `await` in a nested property
collection initializer can report `CS4032`. The diagnostic includes the C#
error that prevents using the expression.

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
