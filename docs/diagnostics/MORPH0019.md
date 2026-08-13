# MORPH0019: Mapping part is configured more than once

## Cause

One mapping configures the same part more than once. A mapping can have only:

- one destination-selection rule: `Construct`, `Resolve`, `ConstructUsing`,
  or `ResolveUsing`;
- one `Members` rule;
- one `Convert` rule.

## Fix

Remove the duplicate call. Merge member assignments into one `Members` lambda,
or choose the single destination-selection rule that represents the required
behavior.

[All diagnostics](../diagnostics.md)
