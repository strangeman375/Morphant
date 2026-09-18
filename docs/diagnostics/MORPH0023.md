# MORPH0023: Mapping setting is not applicable

## Cause

A setting is explicitly configured on a mapping that does not support it:

- `NullSourceHandling`, `NullDestinationHandling`, `ConstructorSelection`,
  `MemberSelection`, `Flattening` or `UnmappedMemberValidation` on a mapping
  with a local `Convert` callback;
- `ConstructorSelection` for a destination without ordinary constructor
  selection, such as a scalar or tuple.

An explicit `Default` is also diagnosed in these cases. Inherited defaults
can be shared with other mappings and do not produce this diagnostic.

## Fix

Remove the setting from the reported mapping. Keep shared defaults at the
mapper or assembly level, and configure the manual behavior inside `Convert`.
The diagnostic points to the setting and the incompatible mapping declaration.

An unused setting is not necessarily invalid: `NullDestinationHandling` is
ignored when Update is disabled, and an explicitly chosen constructor does
not use `ConstructorSelection`.

See [Settings](../settings/README.md#applicability).

[All diagnostics](../diagnostics.md)
