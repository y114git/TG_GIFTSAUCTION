# Changelog

## [1.3.4] - 2026-11-19

### Added

- `execute --timeout <seconds>` stops a script that runs too long. Ctrl+C now also stops a running script and a program started with `execute`.
- `patch merge` prints the output path, the number of merged patches, and the conflict and auto-merged counts.
- `info -v` names the chunk that decided the LTS branch.

### Changed

- Updated to G3MLib 1.1.4: data files, patches and project files are written through a temporary file, so a failed write keeps the old file.
- Scripts run through the G3MLib script runner. A script that uses `#load` sees edits to the loaded files on the next run. `ScriptInputDialog` takes the UndertaleModTool arguments (title, label, default, cancel text, submit text, multiline, prevent close); the older five argument form is gone.
- `execute` with a folder as `--output` (an existing one, or a path ending with a separator) writes the script's exports there and creates it when missing; it no longer writes the data file. With a file path the data file is saved there as before. Without `--output` the exports go to the current folder and no data file is written, where it used to be copied next to the executable.
- The CLI uses the invariant culture for numbers in its output.

### Fixed

- The bundled xdelta is extracted only for commands that can run it. `--help`, `--version` and `info` no longer extract it, and a build without a bundled xdelta for the platform reports it clearly instead of failing.
- `xpatch create`, `xpatch apply` and `execute xdelta` honour `--json` and report errors as JSON.
- A temporary folder that cannot be deleted no longer fails a finished `patch apply`.
- The nested script path error says "script directory" instead of "mod directory".

## [1.3.3] - 2026-11-15

### Changed

- Updated to G3MLib 1.1.3: patches that each add global scripts, tags, feature flags or rooms keep all of them when merged with `--properties`, and patching and merging write data files faster.
- Updated System.CommandLine to 3.0.0 and Roslyn scripting to 5.12.0.
- The G3MLib package version is set in one place (`G3MLibVersion` in `Directory.Build.props`). `-p:G3MLibProject=<path to G3MLib.csproj>` builds the CLI and the GUI against a local G3MLib checkout.

## [1.3.2] - 2026-11-04

### Changed

- Updated to G3MLib 1.1.2.

### Fixed

- Ctrl+C also cancels patch create, patch validate, and info. A second Ctrl+C ends the program at once.
- Batch create stops on Ctrl+C and exits with 130 instead of reporting failed items.
- A temporary folder that cannot be deleted no longer fails a finished command.
- Batch runs treat paths that differ only by case as different files on Linux and macOS.

## [1.3.1] - 2026-11-01

### Changed

- Updated to G3MLib 1.1.0 and System.CommandLine 2.0.14.
- Ctrl+C cancels patch apply, merge, compare, and batch runs. Existing outputs stay untouched, temporary files are removed, and the exit code is 130.

## [1.3.0] - 2026-09-12

### Changed

- Moved patching, merging, diffing, cache handling, and data-file operations to G3MLib. The CLI manages batch jobs and scripts.
- Reduced self-contained single-file release size while retaining platform-specific XDelta support.
- Updated bundled scripts to the G3MLib API and routed their diagnostics through the host.
- Added validated data-file writes and preservation of previous batch outputs.
- Kept default output files beside the published executable and preserved data, patches, and reports across repeated batch merges.
- Fixed nested script execution when the main script is launched with a relative path.
