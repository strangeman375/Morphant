# Unmapped member validation

`UnmappedMemberValidation` selects which unused source and destination members
will be reported by Morphant's configuration diagnostics.

The library default is:

```csharp
UnmappedMemberValidation.None
```

Supported values are:

| Value | Validation scope |
|---|---|
| `Default` | Continue to the next configuration level |
| `None` | Do not require every source or destination member to participate |
| `Source` | Validate supported source members |
| `Destination` | Validate supported destination members |
| `Strict` | Validate supported source and destination members |

Configure an assembly default with the
`MorphantUnmappedMemberValidation` MSBuild property, a mapper default through
`builder.UnmappedMemberValidation(...)`, or one mapping through the pair
builder.

The effective value is selected in this order:

1. The current pair.
2. Included base pairs, nearest first.
3. The current mapper root.
4. Connected base mapper roots, nearest first.
5. A non-`Default` `MorphantUnmappedMemberValidation` MSBuild property.
6. `UnmappedMemberValidation.None`.

The setting already participates in complete inheritance and typed
`IncludeBase` composition. Warning emission is intentionally deferred to Morphant's
diagnostics phase; changing this value does not currently change generated
runtime behavior.

See [Configuration inheritance](../configuration-inheritance.md) for the
composition rules shared by all settings.
