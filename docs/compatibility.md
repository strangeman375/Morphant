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

In a project that references Morphant directly, Morphant checks the referenced
libraries that use Morphant and reports [MORPH0063](diagnostics/MORPH0063.md)
for a library built with an incompatible version. The check sees a library's
dependency on Morphant only in the references passed to the compiler:

- A project that receives Morphant only transitively is not checked. To check
  an application, add a direct reference to the `Morphant` package.
- A project reference is usually compiled against the library's reference
  assembly, which contains only its API. If that API does not use Morphant
  types, for example because the library's mappers are internal, the
  dependency is not visible. Keep the projects of one solution on the same
  Morphant version, for example with central package management.
