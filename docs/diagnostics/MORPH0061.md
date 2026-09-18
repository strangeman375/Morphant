# MORPH0061: Polymorphic branch relationship is not known

## Cause

Morphant cannot determine the relative specificity of `ForDerived` source
types while generic arguments remain unknown. Such a mapping is unsupported.

For example, with a covariant `IProducer<out T>`, these branches change their
relationship depending on the mapper's `T`:

```csharp
.ForDerived<IProducer<T>, GenericResult<T>>()
.ForDerived<IProducer<string>, TextResult<T>>()
```

For `T = object`, the string branch is more specific. Other substitutions can
make the source types equal or unrelated.

The same restriction applies when a branch can become the base source type.

## Fix

Declare the links with closed source types, or use generic branches whose
ordering follows from their declarations and constraints. Separate mappings
can still be called explicitly by their exact source and destination types.

Calling the affected mapping after suppressing this diagnostic throws
`MappingConfigurationException`. Independent valid mappings remain available.

[Runtime polymorphism](../runtime-polymorphism.md) · [All diagnostics](../diagnostics.md)
