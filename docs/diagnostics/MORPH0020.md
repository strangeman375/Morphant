# MORPH0020: Convert cannot be combined with other mapping rules

## Cause

The mapping combines `Convert` with `Construct`, `Resolve`, `ConstructUsing`,
`ResolveUsing`, `Members`, or `IncludeMembers`. `Convert` owns the complete
mapping and leaves no later construction or member step to apply.

## Fix

Choose one approach:

- keep `Convert` and perform the whole mapping in that callback; or
- remove `Convert` and describe construction and members with declarative
  rules.

See [Manual mapping](../manual-mapping.md).

[All diagnostics](../diagnostics.md)
