# MORPH0031: Unsupported mapping expression

## Cause

An inline `Construct`, `Resolve`, or `Members` lambda uses a statement that
Morphant cannot translate into generated mapping code. Examples include loops,
`try`, `goto`, labels, and mutation.

## Fix

Use expressions, initialized locals, complete `if` or `switch` branches,
returns, and throws. If the mapping needs an ordinary algorithm, strict
statement order, or mutation, use `Convert` or a suitable `...Using` callback.

See [Declarative mapping](../declarative-mapping.md).

[All diagnostics](../diagnostics.md)
