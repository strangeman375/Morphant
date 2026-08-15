# Exceptions

Morphant exceptions are in the `Morphant.Exceptions` namespace and derive
from `MorphantException`.

Exceptions tied to one mapping operation also derive from `MappingException`.
They expose the operation, source type and destination type, so application
code does not need to parse exception messages.

## Failure types

| Situation | Exception |
|---|---|
| Invalid mapping configuration | `MappingConfigurationException` |
| Create or Update disabled by `MappingMode` | `MappingOperationNotSupportedException` |
| Null source rejected | `NullSourceException` |
| Null destination rejected | `NullDestinationException` |
| No DI registration for the requested source/destination mapping | `MappingNotFoundException` |
| More than one DI registration for the requested mapping | `AmbiguousMappingException` |
| The only registration resolves to null | `InvalidMappingRegistrationException` |
| `MappingContext.Mapper` used after the top-level `Map` call returned | `MappingScopeCompletedException` |
| Property read from a default `MappingContext` | `InvalidMappingContextException` |
| Current nested value has an incompatible type | `NestedDestinationTypeMismatchException` |
| No branch of a mapping `switch` matches | `UnmatchedMappingSwitchException` |
| `Option<T>.Value` read while empty | `OptionValueMissingException` |
| `Auto`, `Ignore`, `Map`, or another compile-time configuration API is executed as normal code | `RuntimeInvocationNotSupportedException` |

Catch a specific exception when the application can handle it, or catch
`MorphantException` at an application boundary:

```csharp
try
{
    order = mapper.Map(orderDto, order);
}
catch (MappingNotFoundException exception)
{
    logger.LogError(
        exception,
        "Mapping {Source} -> {Destination} is not registered.",
        exception.SourceType,
        exception.DestinationType);
}
```

Morphant does not wrap exceptions thrown by user callbacks, constructors,
member expressions, dependencies or the DI container. They retain their
original type and stack trace.

Ordinary API argument validation follows .NET conventions and may throw
standard exceptions such as `ArgumentNullException`.

See [Compile-time diagnostics](diagnostics.md) for configuration problems
reported during compilation.
