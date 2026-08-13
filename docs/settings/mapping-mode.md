# Mapping modes

`MappingMode` controls which operations a mapping supports. Its default is
`CreateAndUpdate`.

```csharp
builder.Map<OrderDto, Order>(MappingMode.Create);
```

| Value | Create | Update |
|---|---|---|
| `Create` | Available | Throws `MappingOperationNotSupportedException` |
| `Update` | Throws `MappingOperationNotSupportedException` | Available |
| `CreateAndUpdate` | Available | Available |

`Default` inherits the setting. Every generated `ITypeMapper` still implements
both `Create` and `Update`; calling a disabled operation throws immediately.

`MappingMode` also controls manual `Convert` mappings.

Configure a mapper default with:

```csharp
builder.MappingMode(MappingMode.Create);
```

Configure an assembly default with `MorphantMappingMode`. See the
[settings overview](README.md) for levels and precedence.
