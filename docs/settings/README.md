# Settings

Morphant settings can be configured for an assembly, a mapper, or one
source/destination mapping.

| Setting | Default | MSBuild property |
|---|---|---|
| [`MappingMode`](mapping-mode.md) | `CreateAndUpdate` | `MorphantMappingMode` |
| [`NullSourceHandling`](null-handling.md) | `ReturnNull` | `MorphantNullSourceHandling` |
| [`NullDestinationHandling`](null-handling.md) | `Create` | `MorphantNullDestinationHandling` |
| [`UnknownDerivedTypeHandling`](unknown-derived-type-handling.md) | `UseBaseMapping` | `MorphantUnknownDerivedTypeHandling` |
| [`MemberSelection`](member-selection.md) | `Auto` | `MorphantMemberSelection` |
| [`Flattening`](flattening.md) | `Auto` | `MorphantFlattening` |
| [`ConstructorSelection`](constructor-selection.md) | `Unambiguous` | `MorphantConstructorSelection` |
| [`UnmappedMemberValidation`](unmapped-member-validation.md) | `None` | `MorphantUnmappedMemberValidation` |

## Assembly defaults

Set MSBuild properties in a project file or `Directory.Build.props`:

```xml
<PropertyGroup>
  <MorphantNullSourceHandling>Throw</MorphantNullSourceHandling>
  <MorphantUnknownDerivedTypeHandling>Throw</MorphantUnknownDerivedTypeHandling>
  <MorphantMemberSelection>Explicit</MorphantMemberSelection>
  <MorphantFlattening>None</MorphantFlattening>
</PropertyGroup>
```

Names are case-insensitive. A missing, empty or `Default` value continues to
the next configuration level.

## Mapper defaults

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.NullSourceHandling(NullSourceHandling.Throw);
    builder.UnknownDerivedTypeHandling(UnknownDerivedTypeHandling.Throw);
    builder.MemberSelection(MemberSelection.Explicit);
    builder.Flattening(Flattening.None);

    builder.Map<OrderDto, Order>();
    builder.Map<CustomerDto, Customer>();
}
```

Mapper settings apply to every mapping declared in that mapper regardless of
call order. If a setting is written more than once at one level, the last
value wins.

## Mapping overrides

```csharp
builder.Map<OrderDto, Order>(MappingMode.Create)
    .NullSourceHandling(NullSourceHandling.ReturnNull)
    .UnknownDerivedTypeHandling(UnknownDerivedTypeHandling.UseBaseMapping)
    .MemberSelection(MemberSelection.Auto);
```

## Precedence

Each setting is resolved independently:

1. Current mapping.
2. Included mappings, nearest first.
3. Current mapper.
4. Connected base mappers, nearest first.
5. MSBuild property.
6. Morphant default.

`Default` means “continue”, not a separate runtime behavior. Base mapper and
included-mapping values participate only through
[`base.Configure` and `IncludeBase`](../configuration-inheritance.md).

## Applicability

Use supported compile-time constants for settings. Morphant validates values
when the mapping uses the setting; unused settings can be ignored. For
example, `NullDestinationHandling` is ignored when Update is disabled, and
`ConstructorSelection` does not affect an explicitly chosen constructor.

`Convert` owns null handling, construction and member mapping. Inherited
defaults for those behaviors are ignored. Setting them explicitly on the same
mapping as `Convert` produces [`MORPH0023`](../diagnostics/MORPH0023.md), which
lists the unsupported settings. `MappingMode` and `UnknownDerivedTypeHandling`
still apply.
