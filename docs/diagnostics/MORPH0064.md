# MORPH0064: Open mapping extensions can be ambiguous

## Cause

Different registrations produce open extensions that can match the same pair
for type arguments that satisfy the registered types’ constraints, and C#
cannot select a more specific extension.
Each extension has its own type parameters, even within one mapper.
For example, `Box<T, T>` and `Box<U, V>` both match `Box<int, int>`.
Repeating a type parameter does not give the first extension priority.

## Fix

Use distinct DTOs or change the open type patterns so the calls have one
applicable configuration surface. Identical open pairs can share the same
declaration across mappers.

[All diagnostics](../diagnostics.md)
