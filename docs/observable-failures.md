# Observable failures

This page describes the agreed core v0 target. The callback result-policy and
read-only proxy revisions are not yet implemented in the generated API;
current production progress is tracked in the
[mapping API roadmap](../MAPPING_API_IMPLEMENTATION_PLAN.md).

Mapping failures produced by Morphant derive from `MorphantException` in the
`Morphant.Exceptions` namespace. Catch a specific exception when recovery is
meaningful, or catch `MorphantException` at an application boundary. Messages
are deterministic and explain the pair or policy involved, but application
control flow should use the exception type instead of parsing message text.

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
| Incompatible current value for an explicit nested destination | `NestedDestinationTypeMismatchException` |
| No branch matches a declarative switch | `UnmatchedMappingSwitchException` |
| Reading `Option<T>.Value` when `HasValue` is false | `OptionValueMissingException` |
| Direct runtime invocation of a generated-code DSL marker | `RuntimeInvocationNotSupportedException` |

For example:

```csharp
using Morphant.Exceptions;

try
{
    order = mapper.Map(orderDto, order);
}
catch (MappingNotFoundException exception)
{
    logger.LogError(exception, "The order mapping is not registered.");
}
```

## Generated contract completeness

When C# can declare `ITypeMapper<TSource, TDestination>`, an invalid or
unsupported configuration does not make the generated partial mapper empty or
incomplete. Morphant keeps the interface and both methods. Each available
operation keeps its implementation, while an unavailable operation throws the
appropriate typed exception.

This rule also applies to legal roots outside core v0, such as collections,
arrays, tuples, delegates, and async or deferred roots. They receive a
`MappingConfigurationException` stub so a direct cast or manual DI
registration still has deterministic behavior. Morphant does not generate
construction, member, or fluent-extension surfaces for those roots, because
that would imply unsupported mapping semantics.

Some contracts cannot be declared safely in C#. Examples include a non-partial
or file-local mapper, a mapper nested in a non-partial containing type, and two
generic `ITypeMapper<,>` interfaces that can unify for some type argument. Such
contracts are omitted and are candidates for compile-time diagnostics. A
structural problem in one pair does not suppress independent legal pairs in
the same mapper.

Compile-time diagnostics are a separate compatibility surface. Their later
introduction does not change the runtime exception contract for generated
code that can still compile and execute.

## User and dependency exceptions

Morphant does not wrap exceptions thrown by user result-policy, `Members`, or
`Convert` code, by source expressions, by mapper dependencies, or by the
application service provider. Those exceptions retain their original type,
message, stack, and catch behavior. The types above are reserved for mapping
failures authored by Morphant itself and for failures emitted by generated
code.
