# MORPH0064: Open mapping extensions can be ambiguous

## Cause

Two generic mappers register different open pairs that can become the same
pair for some type arguments, and C# cannot select a more specific extension.
For example, `Box<T, T>` and `Box<U, V>` both match `Box<int, int>`.
Repeating a type parameter does not give the first extension priority.

## Fix

Use distinct DTOs or change the open type patterns so the calls have one
applicable configuration surface. Identical open pairs can share the same
declaration across mappers.

[All diagnostics](../diagnostics.md)
