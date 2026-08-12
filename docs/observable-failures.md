# Observable failures

This page documents the implemented core v0 API. Current review status and
remaining boundaries are tracked in the
[mapping API roadmap](../MAPPING_API_IMPLEMENTATION_PLAN.md).

Mapping failures produced by Morphant derive from `MorphantException` in the
`Morphant.Exceptions` namespace. Failures tied to a concrete operation and
exact pair additionally derive from `MappingException`, which exposes
`Operation`, `SourceType`, and `DestinationType`. Catch a specific exception
when recovery is meaningful, or catch `MorphantException` at an application
boundary. Messages are deterministic, but application control flow should use
the exception type and structured properties instead of parsing text.

Ordinary argument validation in handwritten public APIs follows .NET
conventions. For example, constructing `Mapper` with a null service provider
throws `ArgumentNullException` with `ParamName` set to `serviceProvider`.
Such API precondition failures are not part of the Morphant exception
hierarchy.

## Failure types

| Failure | Exception |
|---|---|
| Invalid or unrepresentable mapping configuration | `MappingConfigurationException` |
| Create or Update excluded by `MappingMode` | `MappingOperationNotSupportedException` |
| Null source rejected by policy | `NullSourceException` |
| Null destination rejected by policy | `NullDestinationException` |
| No exact runtime registration | `MappingNotFoundException` |
| More than one exact runtime registration | `AmbiguousMappingException` |
| The only runtime registration resolves to null | `InvalidMappingRegistrationException` |
| Reuse of a completed scoped mapper | `MappingScopeCompletedException` |
| Reading a property of a default-initialized context | `InvalidMappingContextException` |
| Incompatible current value for an explicit nested destination | `NestedDestinationTypeMismatchException` |
| No branch matches a declarative switch | `UnmatchedMappingSwitchException` |
| Reading `Option<T>.Value` when `HasValue` is false | `OptionValueMissingException` |
| Direct runtime invocation of a compile-time DSL intrinsic, including `Value` | `RuntimeInvocationNotSupportedException` |

`MappingConfigurationException` also exposes `Reason`;
`MappingOperationNotSupportedException` exposes `EffectiveMappingMode`; and
`NestedDestinationTypeMismatchException` exposes `ExpectedDestinationType`
and nullable `ActualDestinationType`. Context, `Option<T>`, and DSL-marker
misuse are not tied to one exact pair, so their exception types inherit
directly from `MorphantException`.

A typed `Value<T>`, `Auto<T>`, or `Ignore<T>` whose `T` does not exactly match
its final declarative target is an invalid configuration, even when a broader
intermediate `object` conversion lets the configuration source compile. The
generated exact-pair operation throws `MappingConfigurationException`.
Generated code never invokes a compile-time intrinsic: once Morphant binds one
inside a declarative expression, it must lower every occurrence or reject the
plan. `RuntimeInvocationNotSupportedException` therefore remains a direct API
misuse guard, not a fallback failure from a generated mapper.

For example:

```csharp
using Morphant.Exceptions;

try
{
    order = mapper.Map(orderDto, order);
}
catch (MappingNotFoundException exception)
{
    logger.LogError(
        exception,
        "{Operation} mapping {Source} -> {Destination} is not registered.",
        exception.Operation,
        exception.SourceType,
        exception.DestinationType);
}
```

## Generated contract completeness

When C# can declare `ITypeMapper<TSource, TDestination>`, an invalid
configuration does not make the generated partial mapper empty or incomplete.
Morphant keeps the interface and both methods. Each available operation keeps
its implementation, while an unavailable operation throws the appropriate
typed exception.

Collections, arrays, tuples, delegates, expression trees, buffers, and async,
deferred, or observable roots are valid opaque pairs rather than unsupported
contracts. They receive runtime `ConstructUsing` / `ResolveUsing` and manual
`Convert` extensions, but no structured construction, member, convention, or
special container/deferred semantics. An invalid policy on such a pair still
uses the same complete typed exception-stub rule as any other legal pair.

Some contracts cannot be declared safely in C#. Examples include a non-partial
or file-local mapper, a mapper nested in a non-partial containing type, and two
generic `ITypeMapper<,>` interfaces that can unify for some type argument. Such
contracts are omitted and are candidates for compile-time diagnostics. A
structural problem in one pair does not suppress independent legal pairs in
the same mapper.

Compile-time diagnostics are a separate compatibility surface. Categories
implemented through construction-plan diagnostics `MORPH0035`–`MORPH0039` do
not change the runtime exception contract for generated code that can still
compile and execute; suppression or severity overrides also leave generated
recovery unchanged.

## User and dependency exceptions

Morphant does not wrap exceptions thrown by user result-policy, `Members`, or
`Convert` code, by source expressions, by mapper dependencies, or by the
application service provider. Those exceptions retain their original type,
message, stack, and catch behavior. The types above are reserved for mapping
failures authored by Morphant itself and for failures emitted by generated
code.
