# MORPH0023: Mapping setting is not applicable

## Cause

An explicitly configured setting cannot affect the reported mapping. Common
cases include:

- null handling, constructor selection, member selection, or unmapped-member
  validation on a `Convert` mapping;
- constructor selection where no automatic constructor selection can occur;
- `NullDestinationHandling` when Update is disabled.

## Fix

Remove the setting from that mapping, move it to a mapping where it applies, or
change the mapping mode and rules so the described behavior is reachable. The
diagnostic points to both the setting and the mapping condition that makes it
inapplicable.

See [Settings](../settings/README.md).

[All diagnostics](../diagnostics.md)
