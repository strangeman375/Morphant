# Member selection

`MemberSelection` controls destination members that have no explicit
`Members` rule. Its default is `Auto`.

| Value | Unmentioned destination member |
|---|---|
| `Auto` | Map it by exact-name convention when the conversion is valid |
| `Explicit` | Leave it unchanged |

Explicit rules always take precedence:

```csharp
builder.Map<OrderDto, Order>()
    .MemberSelection(MemberSelection.Explicit)
    .Members((source, _) => new()
    {
        Name = source.DisplayName,
        Revision = Auto(),
        LegacyCode = Ignore()
    });
```

- `Name` uses the explicit expression.
- `Revision` explicitly requests convention mapping.
- `LegacyCode` remains unchanged.

Conventions and `Auto()` require an exact, case-sensitive name and a
warning-free implicit C# conversion. They never start a nested mapping;
use an explicit [`Map`, `Create` or `Update`](../nested-mapping.md).

`MemberSelection` applies only to declarative mappings. A manual `Convert`
owns all member behavior itself.

Configure an assembly default with `MorphantMemberSelection`. See the
[settings overview](README.md) for levels and precedence.
