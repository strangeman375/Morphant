# Generated code

## Configuration surfaces

Morphant ships the root `Map` operation and common settings as instance APIs.
It generates type-specific `Construct`, `Resolve`, and `Members` extension
methods from each declared mapping pair. A mapper identifies its configuration
scope by deriving from `TypeMapper<TMapper>` with itself as `TMapper`.

The generated surface is selected recursively from the pair as written at the
`Map` declaration, before inheritance or generic substitution:

| Declared source/destination pair | Generated surface |
|---|---|
| Contains any type parameter | Mapper-family-scoped |
| Otherwise contains `ValueTuple`, an explicitly nullable reference, or `dynamic` at any depth | Mapper-scoped |
| Otherwise | Shared |

The rule is independent of other mappers in the compilation. Adding or
removing an unrelated mapper therefore does not change an existing surface.
An open pair remains family-scoped when a derived mapper supplies closed type
arguments.

`System.Tuple` alone does not require mapper scope, because it has no C# tuple
element aliases. A nested `ValueTuple`, nullable reference, `dynamic`, or type
parameter still does. Nullable value types such as `int?` remain eligible for
sharing because `Nullable<T>` has a distinct CLR type.

Mapper scope prevents unrelated mappers with different tuple names, nullable
annotations, or `dynamic`/`object` presentations from colliding in generated
configuration code. For a tuple-containing pair, one effective mapper,
including its connected base configuration, must still use one presentation;
a conflict produces [`MORPH0056`](diagnostics/MORPH0056.md).

## Mapper accessibility

Mapper-scoped extensions are emitted as namespace-level generated code. A
type marked with `[MorphantMapper]` and every type containing it must therefore
be accessible from namespace-level code in the same assembly. `public`,
`internal`, and `protected internal` declarations satisfy this requirement;
`private`, `protected`, and `private protected` declarations do not and produce
[`MORPH0059`](diagnostics/MORPH0059.md).

Accessibility is cumulative. For example, a public mapper nested in a private
container is not accessible to generated code.

## View generated code in Rider

Open **Dependencies | Source Generators** in Solution Explorer. This is the
current generated output and requires no project settings.

`Generated/Morphant` is a Git snapshot, not the live Rider view. If Rider shows
stale output, run **Restart Roslyn Analyzers and Source Generators** and reopen
the generated file.

If Morphant catches an internal exception, the **Problems** window reports
[`MORPH0057`](diagnostics/MORPH0057.md) with the failed stage and exception.
Open the named `Morphant.Generated.GeneratorFailure.*.g.cs` file under **Source
Generators** to read the complete stack trace without searching IDE logs.

## Save generated code to Git

Enable the snapshot in the consumer project:

```xml
<PropertyGroup>
  <MorphantGitSnapshot>true</MorphantGitSnapshot>
</PropertyGroup>
```

After a successful compilation, files appear under
`Generated/Morphant/<tfm>`. Failed builds leave the previous snapshot intact,
and snapshot files are excluded from compilation.

The optional settings are:

| Setting | Default | Purpose |
|---|---|---|
| `MorphantGitSnapshotDetail` | `Mappers` | Use `Full` to include all Morphant-generated files. |
| `MorphantGitSnapshotTargetFrameworks` | Last declared TFM | Semicolon-separated subset of the project's TFMs; use `$(TargetFrameworks)` to select all. |
| `MorphantGitSnapshotPath` | `Generated/Morphant` | Dedicated snapshot directory inside the project. |

In multi-target projects, list `TargetFrameworks` from oldest to newest. For
example, `net8.0;net10.0` selects only `net10.0` by default. Every explicitly
selected TFM must also be declared by the project.

Example with all generated files for every TFM:

```xml
<PropertyGroup>
  <MorphantGitSnapshot>true</MorphantGitSnapshot>
  <MorphantGitSnapshotDetail>Full</MorphantGitSnapshotDetail>
  <MorphantGitSnapshotTargetFrameworks>
    $(TargetFrameworks)
  </MorphantGitSnapshotTargetFrameworks>
</PropertyGroup>
```

## Keep the snapshot current

After enabling the feature, changing its settings, or manually deleting or
editing a snapshot file, run:

```bash
dotnet build -c Release -t:Rebuild
```

An up-to-date build may skip compilation and therefore may not repair the
snapshot. Change mappings or models instead of editing generated files.

Debug and Release update the same snapshot; if their output differs, the last
successful build wins. Build the intended configuration before committing.
Changing `MorphantGitSnapshotPath` does not delete the old directory.

For stable line endings across platforms, add:

```gitattributes
**/Morphant.Generated.*.g.cs text eol=crlf
```

Morphant removes obsolete generated files after a successful compilation and
preserves unrelated files in the snapshot directory.

See [Testing mappings](testing.md) for generated-diff checks.
