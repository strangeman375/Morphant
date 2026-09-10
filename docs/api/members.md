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

A constructor parameter and its corresponding destination member share one
value rule. Names match exactly first, then by a unique case-insensitive match.
A `Members` value available before construction overrides the corresponding
constructor argument, including an argument supplied by `Construct` or a branch of
`Resolve`. The overridden argument expression is not evaluated.

During construction, the value is evaluated once and passed to the constructor.
The member is assigned again only when C# requires a `required` initializer;
that assignment reuses the value. Update of an existing object applies
the rule through the writable member; creation-only members keep their values.

Rules for members without a corresponding constructor parameter are applied
to the selected destination.

## Reading `result`

A value needed by an ordinary destination constructor must be available before
that destination exists. Reading `result` in such a value produces
[`MORPH0042`](../diagnostics/MORPH0042.md) on the affected creation paths.

For a mapping that rejects a null Update destination, this rule is valid:

```csharp
builder.Map<Source, Destination>() // Destination(int value)
    .NullDestinationHandling(NullDestinationHandling.Throw)
    .Members((_, _, result, context) => new()
    {
        Value = context.Operation is MappingOperation.Create
            ? 7
            : result.Value + 10
    });
```

An Update may also need construction, for example for a null destination or
a replacement selected by `Resolve`. Read `previous.Value` when a constructor
argument needs the old destination's value, and check that it is present.

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
