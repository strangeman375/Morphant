# MORPH0050: Included source member is ambiguous

## Cause

Two or more `IncludeMembers` scopes expose a readable member with the same
exact name. Morphant does not use call order to choose between them.

## Fix

Remove one of the conflicting scopes, or expose the intended value as a member
on the root source, which has precedence over included scopes. If the removed
scope still needs to supply a value, select that value with an explicit
`Members` expression. An explicit rule does not suppress the ambiguity while
both scopes remain included.

See [Include nested source members](../include-members.md).

[All diagnostics](../diagnostics.md)
