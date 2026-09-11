# `Members`

Defines explicit destination-member rules. Use it for renames, computed
values, ignored members, or explicit nested mappings while leaving other
members to the configured convention.

## Availability

`Members` is available when the destination has at least one supported member:
an assignable property or field, or an eligible readable reference member
that can be updated in place.

## Overloads

Use an inline lambda.

| Callback | Available information |
|---|---|
| `source => rules` | Source |
| `(source, previous) => rules` | Source and existing destination |
| `(source, previous, result) => rules` | Source, existing destination, and selected result |
| `(source, previous, result, context) => rules` | All of the above plus current operation |

Return an object initializer describing the member rules. See [callback inputs](README.md#callback-inputs).

```csharp
builder.Map<OrderDto, Order>()
    .Members((source, _) => new()
    {
        Name = source.DisplayName,
        Revision = Auto(),
        LegacyCode = Ignore()
    });
```

Unmentioned members follow
[`MemberSelection`](../settings/member-selection.md). It can be combined with
one destination method, but not with `Convert`.

## Constructor parameters

Explicit constructor arguments and explicit member rules are independent:

```csharp
builder.Map<Source, Destination>()
    .Construct(source => new(FromConstruct(source)))
    .Members(source => new() { Name = Normalize(source.Name) });
```

Construction evaluates `FromConstruct`; member initialization evaluates
`Normalize`. Identical expressions written in both places also run twice.
Ordinary member initialization uses an object initializer. A constructor marked
`SetsRequiredMembers` does not suppress an explicit member rule.

Automatic constructor arguments can instead use the corresponding member
rule: this applies to `Auto()`, unspecified `ByConvention()` arguments, and
automatic constructor selection. The value is evaluated once and passed to the
constructor; an ordinary setter is skipped. A necessary `required` initializer
reuses that value. Names match exactly first, then by a unique case-insensitive
match.

An explicit `Auto()` member rule after an explicit constructor value remains a
separate member operation. An unmentioned automatic member preserves its
corresponding constructor value. `Ignore()` affects only the location where
it is written; omitted optional constructor arguments keep their defaults.

Reuse skips construction and applies eligible writable member rules.

## Reading `result`

A writable member can read the constructed destination:

```csharp
builder.Map<Source, Destination>()
    .Construct(source => new(source.Value))
    .Members((_, _, result) => new() { Value = result.Value + 10 });
```

An automatic constructor must obtain its arguments independently before a
member value or condition can read `result`. A circular dependency, or an
initializer that needs the not-yet-created result, produces
[`MORPH0042`](../diagnostics/MORPH0042.md).

When member values depend on `result`, Morphant preserves their reads before
writable member assignments, so swapping two members works. Side effects inside
user calls still apply. Source-only rules follow normal initializer or assignment
order. If source and destination are the same object, later source reads can
observe earlier assignments.

An Update may also construct a destination, for example for a null input or
a replacement selected by `Resolve`. `previous` always refers to the original
supplied destination; check that it is present before reading its value.

Results returned by `ConstructUsing` or `ResolveUsing` follow the
[factory result rules](construct-using.md#factory-result): creation-only
members must already be initialized by the callback.

For tuple destinations, `Members` configures tuple elements. See
[Tuple mapping](../tuple-mapping.md) for construction, Update, and factory
behavior for `ValueTuple` and `System.Tuple`.

## Member names

Configuration properties normally keep the destination member's name.
For reserved names, use the alias offered by IntelliSense.
For example, a destination property `Clone` is configured as `Clone_`:

```csharp
.Members(source => new() { Clone_ = source.Clone });
```

The IntelliSense description identifies the original destination member.
Conventions and `Auto()` use the original name.

Related: [declarative expressions](declarative-expressions.md),
[nested mapping](../nested-mapping.md).
