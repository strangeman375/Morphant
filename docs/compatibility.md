# Version compatibility

Code compiled with Morphant, including generated mappers, depends on the
Morphant runtime. An application loads one runtime version, which NuGet selects
for the whole application. A library therefore runs with that version, which
can differ from the one the library was built with.

## Which versions work together

- Before `1.0`, build every library that uses Morphant with the same minor
  version as the application, for example `0.6.x`. Patch releases within a
  minor line are compatible. When the application moves to a new minor
  version, rebuild the libraries with it.
- From `1.0`, the runtime stays compatible with libraries built with earlier
  versions of the same major version. Rebuild the libraries for a new major
  version.

A library built with an incompatible version can fail at run time with
`TypeLoadException` or `MissingMethodException`.

## Build-time check

A project with a direct `Morphant` package reference reports
[MORPH0063](diagnostics/MORPH0063.md) when a library DLL supplied to the
compiler records a dependency on an incompatible Morphant version. Add a
direct package reference to enable the check in an application that receives
Morphant transitively.

For a project reference, an IDE can supply source while you edit. The check
waits until the compiler receives a DLL. An SDK build usually uses a reference
assembly, which can omit a dependency used only in a method body. Keep projects
in the same solution on compatible Morphant versions, for example with central
package management.
