# Unmapped member validation

This page documents the implemented core v0 API. Current review status and
remaining boundaries are tracked in the
[mapping API roadmap](../../MAPPING_API_IMPLEMENTATION_PLAN.md).

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

The selected policy publishes pair-wide completeness warnings after Morphant
has resolved settings, inheritance, construction, members, and nested mapping:

- `MORPH0047` reports a supported source member that is neither used nor
  explicitly discarded by the effective plan.
- `MORPH0048` reports a supported destination member that is not occupied by
  the effective plan.

Both diagnostics are configurable warnings in the
`Morphant.MappingCompleteness` category. They do not change generated code,
runtime behavior, or error recovery. Suppressing a warning or promoting it to
an error changes only compiler presentation.

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
- a `ConstructUsing` or `ResolveUsing` body is ordinary runtime C# and is not
  analyzed as an implicit set of member mappings.

Source participation is semantic. A direct member read, convention or `Auto()`
selection, constructor/member value, condition, or inline runtime callback
read participates. Passing the whole source to opaque code conservatively
counts all supported source members as potentially used. `nameof` and a
type-only test do not read a member; a property pattern reads its referenced
root member normally.

A structured `Construct`, `Resolve`, or `Members` block can explicitly remove
one member from source validation without adding a runtime read:

```csharp
_ = source.LegacyValue;
```

This must be a direct top-level statement for an exact source property or
field. Morphant removes it during lowering, so its getter is not invoked. The
same statement inside `ConstructUsing` or `ResolveUsing` remains ordinary C#
and executes at runtime.

Destination occupancy includes valid convention/explicit member rules,
member-level `Ignore()`, and a passed constructor argument associated with the
member. An omitted optional or `params` argument, constructor-parameter
`Ignore()`, default initialization, `[SetsRequiredMembers]`, reuse of an
existing result, a read-only nested proxy, or a runtime result callback does
not imply occupancy.

`Convert` owns a manual algorithm, so this validation does not apply. An
inherited mapper/root setting is inactive for a manual pair and may still
serve declarative pairs. Writing `UnmappedMemberValidation` explicitly on the
manual pair, including `Default`, reports `MORPH0023`. Both operations of that
pair then throw `MappingConfigurationException` regardless of `MappingMode`.

## Invalid values

C# setting expressions must be compile-time constants defined by
`UnmappedMemberValidation`; the MSBuild property must use one of the named
values above. Morphant reports `MORPH0021` for an effective invalid C#
argument and `MORPH0022` for an effective invalid MSBuild property when at
least one declarative operation is enabled. Runtime mapping remains unchanged;
the affected pair is omitted from completeness analysis. A
valid more-specific value overrides an invalid outer value, and a value used
only by disabled or manual operations is inactive.

`MORPH0023` takes precedence over `MORPH0021` for the same pair-local call.
Suppressing a setting diagnostic or changing its severity changes only
compiler presentation, not runtime behavior or recovery.

See [Declarative mapping](../declarative-mapping.md) for the plan phases whose
effective member use this setting validates.
