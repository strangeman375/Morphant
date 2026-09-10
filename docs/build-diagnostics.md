# Build diagnostics

`MORPHANTMSB...` errors come from the Git snapshot task and Morphant's imported
MSBuild targets. The build message identifies the affected setting or path.
Correct the reported cause, then rebuild the project.

These are MSBuild errors. C# pragmas and `.editorconfig` diagnostic severity
settings do not suppress them.

| Code | Cause | How to fix |
|---|---|---|
| `MORPHANTMSB001` | An unknown operation was passed to the snapshot task. | Use the targets imported by the Morphant package; remove custom direct calls to the task. |
| `MORPHANTMSB002` | Compiler-generated file output is disabled while snapshots are enabled. | Remove the command-line or global override that prevents `EmitCompilerGeneratedFiles` from becoming `true`. |
| `MORPHANTMSB003` | Compiler output and snapshot directories overlap, or a generated path falls outside its output directory. | Use separate directories that are not nested inside one another. Check any linked paths named in the message. |
| `MORPHANTMSB004` | `CompilerGeneratedFilesOutputPath` points to the project directory or an ancestor. | Choose a dedicated compiler-output directory, or remove the custom path override. |
| `MORPHANTMSB005` | The snapshot path is the project directory or an ancestor, belongs to another project, or has a missing or invalid project record. | Use a dedicated snapshot directory per project. Restore a damaged record from Git. If this project was renamed or moved, remove only its snapshot's `.morphant` directory to recreate the record. |
| `MORPHANTMSB006` | A directory setting is empty or invalid on the current operating system. | Supply one valid directory path for the property named in the message. |
| `MORPHANTMSB007` | `TargetFramework` cannot be used as a portable directory name. | Correct the reported TFM value, for example to `net10.0`; do not put a path in `TargetFramework`. |
| `MORPHANTMSB008` | Generated filenames are invalid or collide when compared without case. | Remove stale or manually added conflicting generated files and rebuild. If a clean build produces the same names, report the failure with the filenames. |
| `MORPHANTMSB015` | A file occupies a required directory path, or a directory occupies a required file path. | Move the conflicting item. If the message identifies a previous-format `.morphant` file in the snapshot, remove that file. |
| `MORPHANTMSB016` | A link is broken or cyclic, or a file or directory that Morphant must update is a link. | Repair the broken link, or use a regular file or directory at the reported managed path. |
| `MORPHANTMSB020` | `MorphantGitSnapshotDetail` has an unsupported value. | Set it to `Mappers` or `Full`. |
| `MORPHANTMSB022` | `MorphantGitSnapshot` has an unsupported value. | Set it to `true` or `false`. Use an MSBuild `Condition` to enable it selectively. |
| `MORPHANTMSB999` | An I/O, permission or unexpected task failure prevented the snapshot operation. | Check the reported paths, access permissions and file locks, then rebuild. If the problem persists, report the full error and Morphant version. |

For I/O failures, diagnostic MSBuild verbosity (`-v:diagnostic`) includes the
exception details needed for troubleshooting.

See [Generated code and Git snapshots](generated-code.md) for configuration,
including snapshots for a selected target framework. Mapping-configuration
errors are listed separately in [Compile-time diagnostics](diagnostics.md).
