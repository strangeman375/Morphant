# Unmapped member validation

`UnmappedMemberValidation` selects which unused supported members Morphant's
compile-time configuration diagnostics will validate.

The library default is:

```csharp
UnmappedMemberValidation.None
```

| Value | Validation scope |
|---|---|
| `Default` | Continue to the next configuration level |
| `None` | Do not require every source or destination member to participate |
| `Source` | Validate supported source members |
| `Destination` | Validate supported destination members |
| `Strict` | Validate supported source and destination members |

Diagnostic emission is intentionally deferred to Morphant's diagnostics
phase. The setting already has its final configuration, inheritance, and
applicability model, but changing it does not yet emit warnings or change
generated runtime behavior.

## Configure an assembly default

Set `MorphantUnmappedMemberValidation` in `Directory.Build.props` or a project
file:

```xml
<Project>
  <PropertyGroup>
    <MorphantUnmappedMemberValidation>Destination</MorphantUnmappedMemberValidation>
  </PropertyGroup>
</Project>
```

Supported names are `Default`, `None`, `Source`, `Destination`, and `Strict`,
case-insensitively. A missing, empty, or `Default` value continues to the
library default. MSBuild resolves imports first, so the generator receives the
final property value.

## Configure a mapper default

Use a mapper-level setting when most registrations share one validation
policy:

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.UnmappedMemberValidation(
        UnmappedMemberValidation.Destination);

    builder.Map<OrderDto, Order>();
    builder.Map<CustomerDto, Customer>();
}
```

Mapper-level calls apply regardless of their position relative to `Map`
registrations. The last recognized call wins, including `Default`, which
clears that level and resumes inheritance.

## Override one mapping

Configure the pair builder:

```csharp
builder.Map<OrderDto, Order>()
    .UnmappedMemberValidation(UnmappedMemberValidation.Strict);
```

The effective value is selected independently in this order:

1. A non-`Default` current-pair value.
2. Non-`Default` values from typed included base pairs, nearest first.
3. A non-`Default` current mapper-root value.
4. Non-`Default` connected base mapper roots, nearest first.
5. A non-`Default` `MorphantUnmappedMemberValidation` MSBuild property.
6. `UnmappedMemberValidation.None`.

Base roots participate only after `base.Configure(builder)`, and base-pair
values participate only after
`IncludeBase<TBaseSource, TBaseDestination>()`. See
[Configuration inheritance](../configuration-inheritance.md).

## Validation boundary

The policy applies only to the effective declarative mapping plan built by
Morphant:

- explicit member rules, `Auto()`, and conventions count according to their
  final effective use;
- `Ignore()` deliberately occupies its destination member;
- constructor arguments and creation-time member rules participate;
- overridden, unreachable, or unsupported rules do not become hidden uses;
- read-only proxy members used only for in-place nested Update do not
  participate in ordinary member validation;
- a direct `Construct` body is not analyzed as an implicit set of member
  mappings.

`Convert` owns a manual algorithm, so this validation does not apply. An
inherited mapper/root setting is inactive for a manual pair and may still
serve declarative pairs. Writing `UnmappedMemberValidation` explicitly on the
manual pair is an invalid configuration.

## Invalid values

C# setting expressions must be compile-time constants defined by
`UnmappedMemberValidation`; the MSBuild property must use one of the named
values above. Final diagnostics for invalid values and unmapped members belong
to the separately planned diagnostics phase. Until then, the setting preserves
its deterministic model without defining diagnostic IDs or messages.

See [Declarative mapping](../declarative-mapping.md) for the plan phases whose
effective member use this setting validates.
