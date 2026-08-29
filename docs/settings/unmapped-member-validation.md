# Unmapped member validation

`UnmappedMemberValidation` reports supported members omitted from the final
declarative mapping. Its default is `None`.

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

Unused source members produce
[`MORPH0047`](../diagnostics/MORPH0047.md); destination members that are not
mapped produce [`MORPH0048`](../diagnostics/MORPH0048.md). Both are warnings
and do not change the mapping.

Explicit expressions, conventions, `Auto()`, constructor arguments and nested
rules count according to their actual use. `Ignore()` deliberately occupies a
destination member.

For [`IncludeMembers`](../include-members.md), the selected path counts as
used and the readable members of the included object are checked.

A `Construct`, `Resolve` or `Members` lambda can acknowledge an intentionally
unused source member without reading it when the mapping runs:

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

The discard must appear as a statement on its own in the lambda body. It can
refer to a direct source member, one nested member exposed by
`IncludeMembers`, or an included source object to acknowledge its complete
set of readable members. The getters on that path are not invoked.

`Convert` is a manual algorithm, so unmapped-member validation does not apply
to it.

Configure an assembly default with `MorphantUnmappedMemberValidation`. See the
[settings overview](README.md) for levels and precedence, and
[Diagnostics](../diagnostics.md) for severity configuration.
