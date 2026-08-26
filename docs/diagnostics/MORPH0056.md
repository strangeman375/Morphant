# MORPH0056: Tuple presentation is conflicting

## Cause

Two registrations in the same compilation describe the same CLR source and
destination types with different tuple element names. Tuple names are not part
of CLR type identity, but they do define Morphant's generated declarative API
and name-based mapping plan.

## Fix

Use one consistent source and destination tuple presentation for that physical
mapping pair. If the same CLR pair needs different meanings, introduce
distinct wrapper types. Mapper-scoped tuple surfaces are not currently part of
the API.
