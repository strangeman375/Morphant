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

`Default` inherits the setting. Every generated pair still implements both
`ITypeMapper.Create` and `ITypeMapper.Update`; calling a disabled operation
throws immediately.

The same gate applies to a manual `Convert` mapping.

Configure a mapper default with:

```csharp
builder.MappingMode(MappingMode.Create);
```

Configure an assembly default with `MorphantMappingMode`. See the
[settings overview](README.md) for levels and precedence.
