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

| Constructor argument | Relationship to a matching member rule |
|---|---|
| Explicit value | The argument and explicit member rule are evaluated independently |
| Automatic value (`Auto()`, `ByConvention()` or automatic constructor selection) | An applicable member rule supplies the value once; an ordinary setter is not called again |
| Omitted optional argument | The parameter keeps its default value |

Automatic arguments match member names exactly first, then by a unique
case-insensitive match. A rule supplying an argument must be usable before
construction.

An unmentioned member preserves its corresponding constructor value. An
explicit member rule, including `Auto()`, still applies after an explicit
constructor argument. `Ignore()` affects only the argument or member where
it is written. Reuse skips construction and applies eligible writable rules.

See [nested operation selection](../nested-mapping.md#how-map-chooses-an-operation)
when a constructor or factory supplies a child used by a nested mapping.

## Reading `result`

A writable member can read the constructed destination:

```csharp
builder.Map<Source, Destination>()
    .Construct(source => new(source.Value))
    .Members((_, _, result) => new() { Value = result.Value + 10 });
```

`result` must exist before a rule reads it. A constructor argument or
initializer that requires the not-yet-created destination produces
[`MORPH0042`](../diagnostics/MORPH0042.md).

Member expressions read the initial `result` before writable member
assignments, so swapping two members works. Calls inside those expressions
can still have side effects. Source-only rules follow normal assignment order,
so later reads can observe earlier writes to the same object.

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

Configuration properties normally keep the destination member's name. For a
reserved name, use the alias offered by IntelliSense; its description
identifies the original member. Conventions and `Auto()` use the original name.

Related: [declarative expressions](declarative-expressions.md),
[nested mapping](../nested-mapping.md).
