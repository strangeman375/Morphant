# MORPH0016: Base mapper configuration is unavailable

## Cause

`Configure` calls `base.Configure(builder)`, but the called method body is not
available in the current compilation. This commonly happens when the base
mapper is compiled in another assembly.

## Fix

Keep reusable base configuration in the same project as the derived mapper, or
remove the base call and declare the required mappings and settings in the
current mapper. Cross-assembly configuration inheritance is not supported in
core v0.

See [Configuration inheritance](../configuration-inheritance.md).

[All diagnostics](../diagnostics.md)
