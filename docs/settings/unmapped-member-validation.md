# Unmapped member validation

`UnmappedMemberValidation` reports supported members omitted from the final
declarative mapping plan. Its default is `None`.

| Value | Validation |
|---|---|
| `None` | Disabled |
| `Source` | Check source members |
| `Destination` | Check destination members |
| `Strict` | Check both sides |

```csharp
builder.Map<OrderDto, Order>()
    .UnmappedMemberValidation(UnmappedMemberValidation.Strict);
```

Unused source members produce `MORPH0047`; unoccupied destination members
produce `MORPH0048`. Both are warnings and do not change runtime mapping.

Explicit expressions, conventions, `Auto()`, constructor arguments and nested
rules count according to their actual use. `Ignore()` deliberately occupies a
destination member.

A structured callback can acknowledge an intentionally unused source member
without reading it at runtime:

```csharp
.Members((source, _) =>
{
    _ = source.LegacyValue;

    return new()
    {
        Name = source.Name
    };
});
```

The discard must be a direct top-level statement for a source property or
field. It is a declarative acknowledgement; the getter is not invoked.

`Convert` is a manual algorithm, so unmapped-member validation does not apply
to it.

Configure an assembly default with `MorphantUnmappedMemberValidation`. See the
[settings overview](README.md) for levels and precedence, and
[Diagnostics](../diagnostics.md) for severity configuration.
