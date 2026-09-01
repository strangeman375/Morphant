# MORPH0057: Unexpected generator failure

## Cause

Morphant caught an unexpected exception while generating code. This indicates
a Morphant defect or an incompatible compiler or IDE host, rather than an
invalid mapping configuration.

The diagnostic shown by the compiler and IDE includes the Morphant version,
failed stage, exception type and exception message. Morphant also creates the
named `Morphant.Generated.GeneratorFailure.*.g.cs` file under **Source
Generators**. That file contains the complete exception and stack trace, so IDE
logs are not required.

Generation continues for independent mappings when possible. Output that
depends on the failed stage may be absent.

## Fix

1. Open the generated failure file named in the diagnostic and retain its
   contents.
2. Rebuild the project. In an IDE, restart analyzers and source generators if
   the failure appears to be stale.
3. Update Morphant to the latest available version.
4. If the failure persists, report it with the generated failure file, the
   Morphant version, compiler or IDE version, and a minimal reproduction.

Suppressing `MORPH0057` only hides the error; it cannot restore output from the
failed stage.

Failures that prevent the generator from loading at all, requested
cancellation, and fatal runtime failures cannot be converted to `MORPH0057`.
The compiler or IDE reports those directly, typically as a source-generator
host diagnostic.

[All diagnostics](../diagnostics.md)
