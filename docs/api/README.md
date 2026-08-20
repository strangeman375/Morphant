# Configuration API

Start with [`Map`](map.md). A bare mapping uses conventions; add a method only
when the mapping needs an explicit rule. This reference covers configuration
inside `TypeMapper.Configure`. For application-side `IMapper.Map`, see
[Create and Update](../create-and-update.md).

## Choose a method

| Method | Available when | Use it for |
|---|---|---|
| [`Map`](map.md) | The source and destination form a valid mapping pair | Register the pair and use conventions |
| [`Construct`](construct.md) | The destination has a supported constructor | Supply constructor arguments when no destination exists |
| [`Resolve`](resolve.md) | The destination has a supported constructor | Choose reuse or construction for both Create and Update |
| [`ConstructUsing`](construct-using.md) | Any valid mapping pair | Create through a factory or ordinary C# callback |
| [`ResolveUsing`](resolve-using.md) | Any valid mapping pair | Choose reuse or replacement in ordinary C# |
| [`Members`](members.md) | The destination has at least one supported member | Configure selected destination members |
| [`Convert`](convert.md) | Any valid mapping pair | Own the complete mapping algorithm |
| [`IncludeMembers`](include-members.md) | Any valid mapping pair | Add selected nested source objects to convention lookup |
| [`IncludeBase`](include-base.md) | A compatible mapping is available | Reuse settings and declarative rules |

If `Construct` or `Resolve` is absent, use a `Using` method or `Convert`.
If `Members` is absent, the destination has no member Morphant can assign or
update through a declarative plan.

## Combine methods

Choose at most one destination method: `Construct`, `Resolve`,
`ConstructUsing`, or `ResolveUsing`. `Members` can follow any of them.
`Convert` replaces destination and member rules and cannot be combined with
`Members` or `IncludeMembers`.

Inside declarative callbacks, use
[`Auto`, `Ignore`, `Value`, `ByConvention`, `Map`, `Create`, and `Update`](declarative-expressions.md).

## Settings

Configuration methods also control
[`MappingMode`](../settings/mapping-mode.md),
[null handling](../settings/null-handling.md),
[`MemberSelection`](../settings/member-selection.md),
[`Flattening`](../settings/flattening.md),
[`ConstructorSelection`](../settings/constructor-selection.md), and
[`UnmappedMemberValidation`](../settings/unmapped-member-validation.md).
See the [settings overview](../settings/README.md) for defaults and precedence.
