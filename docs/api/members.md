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

This includes `Auto()` and optional parameters, even when the source has no
matching member for a configured expression. The effective value must satisfy
the constructor parameter's input type and nullable contract. An explicitly
selected constructor remains the selected overload.

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
This includes dependencies through local variables and conditions that select
the value. The generator recognizes operation and `previous.HasValue` checks
in conditional expressions, `if`, and `switch`, including boolean combinations
and local aliases of those checks.

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

With the default null-destination setting, `Update(source, null)` also needs
construction, while `context.Operation` remains `Update`. The same expression
then produces MORPH0042 for that case. When the mapping reuses every supplied
destination, `previous.HasValue ? result.Value + 10 : 7` handles both creation
paths.

If `Resolve` selects a replacement, `previous.HasValue` does not establish
that the new `result` exists. Read `previous.Value` when the constructor should
use the old object's value. Disabled operations and null destinations rejected
by settings do not contribute creation paths to this check. Suppressing the
diagnostic keeps a `MappingConfigurationException` on the invalid path;
valid creation and reuse paths remain available.

With `Construct` or a construction branch of `Resolve`, Morphant owns object
creation and can place creation-only rules in the initializer. A result returned
by `ConstructUsing` or `ResolveUsing` is already initialized and is not
reconstructed. Its settable members and eligible readable nested members remain
available, but an `init`-only rule produces
[`MORPH0042`](../diagnostics/MORPH0042.md).

For tuple destinations, `Members` configures tuple elements. See
[Tuple mapping](../tuple-mapping.md) for construction, Update, and factory
behavior for `ValueTuple` and `System.Tuple`.

## Member names

Configuration properties normally keep the destination member's name.
If it conflicts with the generated record's name, a type parameter or a
record member, Morphant appends underscores until the name is free.
For example, a destination property `Clone` is configured as `Clone_`:

```csharp
.Members(source => new() { Clone_ = source.Clone });
```

If the destination also declares `Clone_`, that property keeps its name and
`Clone` uses `Clone__`. IntelliSense describes the destination member each
property represents. The same rule applies to tuple elements.
Conventions and `Auto()` use the original destination name; `Ignore()`,
`with` overlays and nested updates work through the configuration alias.

Related: [declarative expressions](declarative-expressions.md),
[nested mapping](../nested-mapping.md).
