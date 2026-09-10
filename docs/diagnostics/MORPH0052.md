# MORPH0052: Polymorphic mapping cannot link to itself

## Cause

`ForDerived` uses the exact source type of its base mapping. A branch must
select a different runtime source type, even when its destination differs.

## Fix

Remove the link or replace its source with a genuinely derived class,
implementation or compatible value type. The exact base source already uses
the base mapping.

[All diagnostics](../diagnostics.md)
