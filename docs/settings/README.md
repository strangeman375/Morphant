# Settings

Morphant settings can be configured for an assembly, a mapper, or one mapping
pair.

| Setting | Default | MSBuild property |
|---|---|---|
| [`MappingMode`](mapping-mode.md) | `CreateAndUpdate` | `MorphantMappingMode` |
| [`NullSourceHandling`](null-handling.md) | `ReturnNull` | `MorphantNullSourceHandling` |
| [`NullDestinationHandling`](null-handling.md) | `Create` | `MorphantNullDestinationHandling` |
| [`MemberSelection`](member-selection.md) | `Auto` | `MorphantMemberSelection` |
| [`ConstructorSelection`](constructor-selection.md) | `Unambiguous` | `MorphantConstructorSelection` |
| [`UnmappedMemberValidation`](unmapped-member-validation.md) | `None` | `MorphantUnmappedMemberValidation` |

## Assembly defaults

Set MSBuild properties in a project file or `Directory.Build.props`:

```xml
<PropertyGroup>
  <MorphantNullSourceHandling>Throw</MorphantNullSourceHandling>
  <MorphantMemberSelection>Explicit</MorphantMemberSelection>
</PropertyGroup>
```

Names are case-insensitive. A missing, empty or `Default` value continues to
the next configuration level.

## Mapper defaults

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.NullSourceHandling(NullSourceHandling.Throw);
    builder.MemberSelection(MemberSelection.Explicit);

    builder.Map<OrderDto, Order>();
    builder.Map<CustomerDto, Customer>();
}
```

Mapper settings apply to every local pair regardless of call order. If a
setting is written more than once at one level, the last value wins.

## Pair overrides

```csharp
builder.Map<OrderDto, Order>(MappingMode.Create)
    .NullSourceHandling(NullSourceHandling.ReturnNull)
    .MemberSelection(MemberSelection.Auto);
```

## Precedence

Each setting is resolved independently:

1. Current pair.
2. Included base pairs, nearest first.
3. Current mapper.
4. Connected base mappers, nearest first.
5. MSBuild property.
6. Morphant default.

`Default` means “continue”, not a separate runtime behavior. Base mapper and
pair values participate only through explicit
[`base.Configure` and `IncludeBase`](../configuration-inheritance.md).

Setting arguments must be compile-time constants. Invalid values or settings
that do not apply to a pair produce a compile-time diagnostic; suppressing the
diagnostic does not make the configuration valid.
