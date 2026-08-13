# MORPH0024: Duplicate base configuration call

## Cause

The same base `Configure` method is included more than once while configuring a
mapper. Applying it repeatedly would duplicate its settings and mapping
declarations.

## Fix

Keep a single `base.Configure(builder)` path for each base mapper level. Remove
the extra call and let each override include its immediate base configuration
once.

See [Configuration inheritance](../configuration-inheritance.md).

[All diagnostics](../diagnostics.md)
