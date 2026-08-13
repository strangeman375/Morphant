# MORPH0013: Duplicate mapping registration

## Cause

The same source/destination pair is registered more than once in one mapper.
Different fluent chains do not create separate mappings for the same pair.

## Fix

Keep one `Map<TSource, TDestination>()` call and combine its settings and rules
on that registration. Registrations in different mapper types remain separate.

[All diagnostics](../diagnostics.md)
