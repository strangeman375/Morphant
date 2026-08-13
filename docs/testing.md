# Testing mappings

Test mappings through the same public entry point used by the application.
For DI-based applications, resolve `IMapper` from the test service scope and
verify the returned values:

```csharp
[Test]
public void Maps_an_order()
{
    var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
    var source = new OrderDto { Id = 17, Name = "New" };

    var created = mapper.Map<OrderDto, Order>(source);

    Assert.That(created.Id, Is.EqualTo(17));
    Assert.That(created.Name, Is.EqualTo("New"));
}
```

Use the same mapper registrations as the application so the test also covers
runtime mapping selection and nested mappings.

## Test Update separately

Create and Update have different destination rules. Test whether Update should
reuse or replace its destination, and always assert against the returned value:

```csharp
var existing = new Order { Id = 17, Name = "Old" };

var result = mapper.Map(source, existing);

Assert.That(result, Is.SameAs(existing));
Assert.That(result.Name, Is.EqualTo("New"));
```

Also cover null behavior when the mapping changes the default
[`null-handling settings`](settings/null-handling.md).

## Check compile-time diagnostics

An ordinary `dotnet build` runs the source generator and reports invalid
mapping configuration. Keep it in CI. If completeness warnings are enabled,
choose their severity through `.editorconfig` as described in
[Compile-time diagnostics](diagnostics.md).

## Review generated code

When generated files are committed, review their diff together with mapping
configuration changes. Behavior tests remain the primary check; generated
files make unexpected mapping changes visible before merge.

See [Generated code](generated-code.md) for project configuration.
