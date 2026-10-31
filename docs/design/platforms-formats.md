# Platforms and data formats

Status: proposed. The operating systems G3MStudio runs on, the in-app updater, the bundled scripts folder, how several windows and processes behave, and the data file platforms and formats it opens: containers, chunks, bytecode versions, GameMaker version detection, YYC games, write checks, debug data and the offset map. Requirements: N-PLT-1, F-UPD-1 to F-UPD-5, F-PAR-1. Shared rules: [ux-standards.md](ux-standards.md). The binary format itself is documented in [../research/gamemaker-format.md](../research/gamemaker-format.md) and [../research/gamemaker-format-coverage.md](../research/gamemaker-format-coverage.md); this page is the user-facing behaviour.

## 1. Purpose and entry points

UTMT is a Windows program that opens `data.win`. G3MStudio opens the same files on three systems, updates itself, and tells the user exactly what it found in the file. Entry points: Help > About G3MStudio and Check for Updates, Settings > Updates and Paths, the status bar item "Data file info" (platform, GameMaker and bytecode version), General Info, Tools > Check Data File Integrity and Tools > Generate Offset Map.

## 2. Layout

About dialog (`help.about`):

```
+----------------------------------------------+
| [logo]  G3MStudio 1.0.0-beta                 |
|         Channel: beta   Build 4521           |
|         G3MLib 1.1.0   .NET 10.0.0   x64     |
|         Windows 11 10.0.26300                |
|  Reads GameMaker data up to 2026.1, bytecode |
|  13 to 17.                                   |
|  GPL-3.0-only    [Licences] [Release Notes]  |
|               [Copy Details]  [Close]        |
+----------------------------------------------+
```

The "Data file info" status item and the General Info "Format" group (editors/general-info.md) show the file's platform, GameMaker version, bytecode version and whether it is a YYC build.

## 3. Platforms and the GUI (UTMT-PLAT-02, N-PLT-1)

UTMT's GUI is WPF on .NET for 64-bit Windows and uses Windows Forms, DWM and registry calls. G3MStudio is Avalonia on .NET, so the GUI runs on:

| System | Architectures | Notes |
| --- | --- | --- |
| Windows 10 22H2 and 11 | x64, arm64 | Reference platform; custom title bar with system caption buttons |
| Linux (glibc, X11 and Wayland) | x64, arm64 | Portal file dialogs where available; `.desktop` and MIME registration |
| macOS 13 and later | x64, arm64 | Native menu bar; unsigned (Q7) with documented first-run steps |

Rules:

1. Every Windows-only call in UTMT has a platform service with one interface and three implementations: dark title bar (DWM immersive dark mode on Windows 11; NSAppearance on macOS; the desktop's theme on Linux), file associations (registry, MIME, document types), shell verbs, single instance, "Show in file manager", and the Wine command for running Windows games ([run-game.md](run-game.md) section 3). A missing implementation disables the feature with a tooltip, never a crash.
2. No Windows Forms. Dialogs are Avalonia and system pickers through the storage provider.
3. Data files are platform independent: a macOS user can edit `data.win` from a Windows game. Paths inside data files use `/` and `\` as the files store them and are never rewritten on save.
4. Every release is tested on each system and architecture before it ships (N-PLT-1); the build matrix is in the roadmap.
5. File name case: Windows and macOS are case-insensitive by default, Linux is not. Lookups of game files next to a data file try the exact name, then a case-insensitive match, and the Output channel says when the second was used.
6. Font and text rendering differences are covered by ux-standards 13.
7. Distribution (Q27): six portable zips, one per system and architecture, named `G3MStudio-<OS>-<ARCH>-<VERSION>.zip`. Each is self-contained (no runtime install) with the files in the zip root, not inside a folder in the zip. There are no installers and no native packages for beta. File associations, the URL scheme and shortcuts are registered per user by the app itself (onboarding step 4), never by an installer. All user data lives in the per-user app data folder (architecture.md "Files on disk"), never next to the program, so replacing the program folder loses nothing. The first launch may be slow; the build is kept as light as possible without losing performance or quality (N-SIZE-1).

G3MStudio is a GUI program with no command line (questions.md Q24). Scripting and automation use G3MTool (CLI and GUI). Support is full on every system above (N-PLT-2, architecture.md "Platform support"); a feature is blocked on one system only with evidence in [../research/platform-proof-log.md](../research/platform-proof-log.md).

## 4. In-app updater (UTMT-PLAT-03, F-UPD-1 to F-UPD-4)

UTMT updates only in debug (nightly) builds, from the latest GitHub Actions artifact, and never checks by itself. G3MStudio updates every build, from the GitHub releases of the owner's repository (Q28, Q33). The updater is G3MStudio's own ([../decisions/0005-updater.md](../decisions/0005-updater.md)).

### 4.1 Flow

1. Check at start (default, `updates.checkOnStart`), on demand (`help.checkUpdates`) and every 24 hours while running. A check reads the releases of the owner's GitHub repository over HTTPS and sends no data about the user or the files (GitHub sees the IP address; onboarding principle 4). The highest of the latest release, the latest pre-release (only when `updates.channel` is `prerelease`) and the running version wins. If the running version is the highest nothing happens; there is never a downgrade.
2. A newer version shows the notification "G3MStudio {version} is ready to install." with "What's New" and "Restart to Update" (shell 8). The download of `G3MStudio-<OS>-<ARCH>-<VERSION>.zip` for the detected system and architecture runs in the background (`updates.autoDownload`), over HTTPS from the repository's releases only. The file is compared with GitHub's SHA-256 digest for that asset; on a mismatch it is deleted, the update is refused and `G3MS-UPD-002` is reported (F-UPD-2). There is no code signing and no signature scheme; the trust root is the owner's GitHub account.
3. Free space: the download needs the zip size plus the unpacked size on the program's drive and on the temp drive; the check names the shortfall ("The update needs 640 MB free on drive C:. Free some space, then try again."). UTMT's fixed 500 MB threshold is replaced by this computed amount.
4. "Restart to Update" saves everything first (F-UPD-4). Project changes are saved automatically; if changes are not yet in the data file, the prompt of shell 14.2 item 8 appears with Sync changes, Do it later or Cancel (Cancel cancels the update). Running games started by G3MStudio are not touched; the dialog says "A game is running. It keeps running."
5. A small updater program, shipped in the zip root and copied to a temporary folder so it can replace itself, waits for G3MStudio to close, extracts all files of the zip into the folder of the previous executable, replaces them and starts the new executable. Windows, workspaces and layouts return (shell 13.5). The first start after an update shows one toast "Updated to {version}." with "What's New".
6. Rollback (F-UPD-2): the replaced files are kept in the user data folder. If replacing fails part way, the updater restores them and the old version starts. If the new version fails to start twice in a row, the next start runs the previous one and shows "G3MStudio {version} failed to start. The previous version was restored." `help.revertVersion` ("Revert to Previous Version...") does it on request.
7. Pre-releases (F-UPD-3): `updates.channel` is `stable` (default) or `prerelease`; the off switch is `updates.checkOnStart` = false.
8. Program folder not writable (F-UPD-5): if the updater cannot write to the folder of the executable (Program Files, a read-only volume, a protected location), nothing is replaced. The download stays in the user data folder and the message names its path, so the user can extract it over the program folder by hand (see 4.2).

### 4.2 Messages

| Case | Message |
| --- | --- |
| Up to date | "G3MStudio {version} is up to date." |
| No network | "Could not check for updates. Check your connection." (Info, not an error) |
| Verification failed | "The update file did not match its checksum and was deleted." (`G3MS-UPD-002`) |
| Not enough space | The text of 4.1 step 3 |
| Install failed | Error with "Copy Details" and "Open Release Page"; the old version keeps running |
| Program folder not writable | "G3MStudio cannot write to {folder}. The update was downloaded to {zip path}. Extract it over the program folder yourself, or move G3MStudio to a folder you can write to." with "Open Folder" and "Copy Path" |
| Changes not in the data file | The prompt of shell 14.2 item 8: Sync changes, Do it later, Cancel |

## 5. Scripts beside the program (UTMT-PLAT-04)

G3MStudio ships no scripts (Q42; [scripting.md](scripting.md) section 6, [script-features.md](script-features.md)). The program folder has no `Scripts/` folder. The Scripts menu reads the user scripts folder in the user data folder and, in project mode, the project's `scripts/` folder.

1. The user folder is created on first use ("New Script File..." or "Open Scripts Folder").
2. The menu is built from the folders at run time, so a script dropped into one appears after Refresh Script List.
3. Every script asks for trust on its first run ([scripting.md](scripting.md) section 8); there is no release manifest hash for scripts.
4. Game specific patches from the old UTDR scripts are plugin candidates ([script-features.md](script-features.md) section 5), delivered as plugins with their own data folders.

## 6. Several windows, one process (UTMT-PLAT-05)

UTMT starts a new process for each data file and for each external audio group file, linked by a named pipe. G3MStudio uses one process with several windows (shell 13, F-WS-9):

1. Each window holds one workspace. New Window, Open in New Window and a second launch with a file all open a window in the running process.
2. Audio group files (`audiogroupN.dat`) open as part of the main workspace's Audio Groups and in a linked tab, not as a second process (UTMT-FILE-11). The pipe is not needed.
3. A second launch while G3MStudio runs forwards its arguments over a local socket (named pipe on Windows) and exits 0. The key is per user. `--new-instance` forces a separate process with its own settings folder (`--profile <name>`), for testing builds.
4. Two processes (for example two profiles) cannot edit the same data file: a lock file in app data, keyed by the file identity, marks the writer. The second opens it read-only: "This file is open for editing in another G3MStudio. You can compare it here but not change it." (shell 13.3).
5. There is no multi-file workspace: one workspace is one data file or one project, as in UTMT. Related files (audio groups, included files) belong to it.
6. Crash of one window takes down the process; the journal of every workspace (F-WS-4) is intact, and the next start restores all windows.

## 7. Data file formats

### 7.1 Containers (UTMT-FMT-01)

G3MStudio opens the IFF `FORM` container GameMaker uses on every platform, recognized by content and not by name.

| File | Platform | Open dialog filter |
| --- | --- | --- |
| `data.win` | Windows | Yes |
| `game.unx` | Linux | Yes |
| `game.ios` | iOS and macOS | Yes |
| `game.droid` | Android | Yes |
| `game.win` | Older Windows builds and some tools | Yes |
| `audiogroupN.dat` | External audio groups | Yes |
| `.3ds`, `.symbian` | Associated by UTMT; same container | Yes, as "Other data files" |
| Any file starting `FORM` | Any | Through "All files" |

Other formats G3MStudio opens are not data files: `.g3mpatch`, `.xdelta`, `.vcdiff`, `.g3mproj`, project JSON, `.csx` and mod `.zip` (they open the matching prompt, shell 14.1). A file that is none of these shows "'{name}' is not a GameMaker data file. Choose a file named data.win, game.ios, game.unx or game.droid, or an audiogroup file." (`G3MS-LOAD-002`).

Game folder detection for the start page and onboarding uses the file names above (G3M-36).

### 7.2 Chunks (UTMT-FMT-02)

G3MStudio reads and writes every chunk G3MLib supports, up to GameMaker 2026.1. Each chunk has an editor or viewer, listed here. Unknown chunks are kept byte for byte, listed in the Explorer under Data as "Unknown chunks" with name, offset and size (read-only), and written back unchanged (G3MLib work item 2); they never block opening or saving.

| Chunk | Contents | Spec |
| --- | --- | --- |
| FORM | Container header | this page |
| GEN8 | General info | [editors/general-info.md](editors/general-info.md) |
| OPTN | Game options | [editors/general-info.md](editors/general-info.md) |
| LANG | Languages | [editors/languages.md](editors/languages.md) |
| EXTN | Extensions | [editors/extensions.md](editors/extensions.md) |
| SOND | Sounds | [editors/sounds.md](editors/sounds.md) |
| AGRP | Audio groups | [editors/sounds.md](editors/sounds.md) |
| SPRT | Sprites | [editors/sprites.md](editors/sprites.md) |
| BGND | Backgrounds and tile sets | [editors/backgrounds-tilesets.md](editors/backgrounds-tilesets.md) |
| PATH | Paths | [editors/paths.md](editors/paths.md) |
| SCPT | Scripts | [editors/scripts.md](editors/scripts.md) |
| GLOB | Global init scripts | [editors/global-scripts.md](editors/global-scripts.md) |
| GMEN | Game end scripts | [editors/global-scripts.md](editors/global-scripts.md) |
| SHDR | Shaders | [editors/shaders.md](editors/shaders.md) |
| FONT | Fonts | [editors/fonts.md](editors/fonts.md) |
| TMLN | Timelines | [editors/timelines.md](editors/timelines.md) |
| OBJT | Game objects | [editors/objects.md](editors/objects.md) |
| ROOM | Rooms | [editors/rooms.md](editors/rooms.md) |
| UILR | UI layers | [editors/rooms.md](editors/rooms.md) section 8 |
| DAFL | Data files | [editors/tags-feature-flags.md](editors/tags-feature-flags.md) |
| TPAG | Texture page items | [editors/texture-pages.md](editors/texture-pages.md) |
| CODE | Code | [editors/code.md](editors/code.md) |
| VARI | Variables | [editors/variables-functions.md](editors/variables-functions.md) |
| FUNC | Functions and code locals | [editors/variables-functions.md](editors/variables-functions.md) |
| STRG | Strings | [editors/strings.md](editors/strings.md) |
| TXTR | Embedded textures | [editors/texture-pages.md](editors/texture-pages.md) |
| AUDO | Embedded audio | [editors/sounds.md](editors/sounds.md) |
| EMBI | Embedded images | [editors/embedded-images.md](editors/embedded-images.md) |
| TGIN | Texture group information | [editors/texture-pages.md](editors/texture-pages.md) |
| ACRV | Animation curves | [editors/animation-curves.md](editors/animation-curves.md) |
| SEQN | Sequences | [editors/sequences.md](editors/sequences.md) |
| TAGS | Tags | [editors/tags-feature-flags.md](editors/tags-feature-flags.md) |
| FEDS | Filter effects | [editors/filter-effects.md](editors/filter-effects.md) |
| FEAT | Feature flags | [editors/tags-feature-flags.md](editors/tags-feature-flags.md) |
| PSEM | Particle system emitters | [editors/particle-systems.md](editors/particle-systems.md) |
| PSYS | Particle systems | [editors/particle-systems.md](editors/particle-systems.md) |

A chunk missing from the file means its Explorer node is hidden (shell 5.2). Chunk order is kept as the file has it.

### 7.3 Bytecode versions (UTMT-FMT-03)

1. Bytecode 13 to 17 is fully supported: decompile, compile, assemble, disassemble and save. Versions 14 and lower use the older code layout; G3MLib selects the layout from the version and the Code editor shows it in its status ("Bytecode 14").
2. Any other version (below 13 or above 17) loads with code disabled and saving refused (UTMT-FILE-05, shell 14.1): resources outside code can still be viewed, and the info bar offers Save As to a copy that has the code chunk unchanged only when G3MLib can write it byte for byte; otherwise Save is disabled with the reason.
3. The version shows in the status bar ("Bytecode 17") and in General Info with the platform. `bytecodeSupported` is a context key for commands (shell 3.1).
4. The bytecode converters (13 to 14, 14 to 16, 15 to 16, 15 or 17 to 16, 16 to 17, 17 to 16 and the GMS 2.3 variant) ship as scripts ([scripting.md](scripting.md) section 6) and are listed in Tools as "Convert Bytecode Version...", which runs the right script after a confirmation that names the risk ("Converted files may not run on every runner.").

### 7.4 GameMaker version detection (UTMT-FMT-05)

GEN8 headers stop at 2.0, so the real version is inferred from the layouts of chunks. G3MStudio shows the result and how it was found.

1. Detection points: 2.0.6, 2.2.2.302, 2.3, 2.3.1, 2.3.2, 2.3.7, 2022.1, 2022.2, 2022.3, 2022.5, 2022.6, 2022.8, 2022.9, 2023.1, 2023.2, 2023.4, 2023.6, 2023.8, 2023.11, 2024.2, 2024.4, 2024.6, 2024.8, 2024.11, 2024.13, 2024.14, 2024.14.1, 2024.14.4 and 2026.1 (beta). The 2022 LTS branch is detected separately from the main line (an LTS file that looks like 2022.2 on the main line is labelled "2022 LTS").
2. The status bar and the About group show "GameMaker 2023.8". The General Info row "GameMaker version" has a "Details..." link that opens the detection report: every detection point, whether it was reached, and the chunk and field that decided it ("2023.2: SPRT has a nine slice flag"). The report can be copied.
3. A version is a lower bound when the file has no feature that marks the next one. The label then reads "GameMaker 2023.8 or later" and the report says which feature is missing.
4. Detection runs on load, off the UI thread, and its result is stored in the model so editors show only the fields that exist for that version.
5. A manual override ("Set GameMaker Version...", General Info only) exists for files where detection is wrong; it warns that wrong values can make the saved file unreadable, is saved per workspace and is cleared by Revert to Saved.
6. Files newer than the newest known point show "Newer than GameMaker 2026.1. Some fields are kept but not shown." and the unknown parts round trip (G3MLib passthrough).

### 7.5 YYC games (UTMT-FMT-06)

A game built with the YoYo Compiler has no code in the data file. G3MStudio detects it and loads and saves all assets.

1. The info bar of shell 14.1 appears on open; the status bar "Data file info" adds "YYC".
2. Disabled with the reason "This game was built with the YoYo Compiler and has no code in the data file.": Code in the Explorer (the node is hidden), code search ("Search refused: ..." shell 7.7), compile and decompile, `dump -c` and `replace -c` (exit 4), script code APIs (they return empty text).
3. Everything else works: sprites, rooms, objects (their event list, not the code), sounds, fonts, strings, texture pages, saving, patches, launch.
4. Launch works with the original runner because the game code is in the executable. A mod that changes assets and not code works; changes to object events have no effect and Problems says "YYC games ignore object events."
5. Support beyond this is an open research issue in the format notes; no behaviour is promised.

## 8. Writing and integrity (UTMT-FMT-10)

Writing is the point where data is lost, so it is checked twice (N-SAFE-1).

1. Writer checks. The writer verifies that every object referenced by another was written (`ThrowIfUnwrittenObjects`) and applies the alignment rules per chunk (for example 4-byte alignment of texture page items, audio and strings per version). A violation stops the save with `G3MS-SAVE-003` and names the chunk and object: "Could not save. The object 'spr_player' (SPRT, index 87) is referenced but was not written."
2. Validation read. Save writes to a temp file, reads it back with G3MLib, compares chunk counts and hashes with the model, then replaces atomically (shell 14.2). Failure leaves the file untouched (`G3MS-SAVE-002`).
3. Tools > Check Data File Integrity (`tools.integrity`, `g3mstudio integrity`) runs the same checks on demand on the open workspace or a chosen file, without saving:

| Check | Meaning |
| --- | --- |
| Round trip | The model is written to memory and read back; any difference is listed by chunk, except fields known to change (the GEN8 build number) |
| Unwritten objects | Objects that are referenced but would not be written |
| Pointer targets | Every pointer resolves inside the right chunk; null pointers where the format requires a value |
| Alignment | Chunk and entry alignment per version |
| Duplicates and names | Duplicate resource names, names that are not valid identifiers |
| References | Indexes that point past the end of a list, sprites and sounds missing, code entries without locals |
| Decompile pass | Optional: decompile every code entry and report failures (replaces `CheckDecompiler.csx`) |

Results go to Problems with source "Integrity". Each finding with a safe automatic fix has a Fix button (for example "Recreate missing code locals", "Remove null reference"); "Fix All Safe" applies them as one undo step. Failures that need a decision stay unfixed with an explanation.
4. Every finding has a stable code `G3MS-INT-nnn` shown after Copy Details.

## 9. Debug data and the offset map

### 9.1 Debug data format (UTMT-FMT-09)

`.yydebug` is the file the GameMaker 1.4 debugger reads. G3MStudio writes it from the open workspace for [run-game.md](run-game.md) section 6: a header with the file name and version, the list of code entries with their source text (decompiled source or disassembly, by the chosen mode) and the byte offset of each line for breakpoints, and the list of objects and rooms the debugger shows. "No debug data" writes the header and empty tables. The writer is G3MLib's debug chunk writer; its output is deterministic. Reading `.yydebug` files is not offered.

### 9.2 Pointer offset map (UTMT-FMT-11, UTMT-FILE-10)

`tools.offsetMap` ("Generate Offset Map...", `g3mstudio offset-map`) reads a data file and writes a text file `<file>.offsetmap.txt`. It does not need the file to be open, and does not change it.

1. Choose the file (the open one is preselected). The command runs as a job with progress and Cancel; a 92 MB file takes about a second.
2. Output: one line per serialized object, in file order: `0x0001A2B0  SPRT  Sprite 'spr_player'  (88 bytes)`. Offsets are uppercase hex with a `0x` prefix, monospaced and aligned; columns are offset, chunk, object kind and name, length. Pointers to objects that were never written print as `0x00000000  NULL  (referenced by ...)`, which is how null holes are found.
3. When done a notification "Offset map written." offers "Open" (the file opens in the text viewer of the code editor, read-only) and "Show in File Manager".
4. A line of the map opened from the Output channel or Problems is a link: Ctrl+click on an offset in any hex text goes to the object in the Explorer when one exists.
5. Existing files are not overwritten without a confirmation (the offset map of a large game can be 30 MB).
6. The map is also generated when saving fails with `G3MS-SAVE-003`: "Create Offset Map" is offered as an action in the error dialog.

## 10. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| About G3MStudio | `help.about` | None | None |
| Check for Updates... | `help.checkUpdates` | None | None |
| Revert to Previous Version... | `help.revertVersion` | None | None |
| Release Notes | `help.releaseNotes` | None | None |
| Check Data File Integrity | `tools.integrity` | None | None |
| Fix All Safe Integrity Findings | `tools.integrityFix` | None | None |
| Generate Offset Map... | `tools.offsetMap` | None | None |
| Convert Bytecode Version... | `tools.convertBytecode` | None | None |
| Set GameMaker Version... | `generalInfo.setVersion` | None | None |
| Show Version Detection Report | `generalInfo.versionReport` | None | None |

## 11. Validation and messages

| Case | Message |
| --- | --- |
| Not a data file | "'{name}' is not a GameMaker data file. Choose a file named data.win, game.ios, game.unx or game.droid, or an audiogroup file." |
| Unsupported bytecode | "Bytecode version {n} is not supported. Code is read-only and saving is disabled." |
| Newer than known | "Newer than GameMaker 2026.1. Some fields are kept but not shown." |
| YYC | The info bar text of shell 14.1 |
| Save integrity | `G3MS-SAVE-002` and `G3MS-SAVE-003` texts of section 8 |
| Offset map exists | "'{file}' already exists. Replace it?" |
| Bundled scripts missing | Text of section 5 step 2 |
| Update verification | Texts of section 4.2 |

## 12. Undo

Version overrides and integrity fixes are undo steps ("Set GameMaker Version", "Fix {n} Integrity Findings"). Updates, offset maps and conversions of bytecode are not undoable except the conversion, which is a script run with a checkpoint ([scripting.md](scripting.md) section 3.4). About, update checks and detection reports change nothing.

## 13. Empty and loading states

- Unknown chunks: when there are none, the node is hidden.
- Integrity results with no findings: "No problems found in 'data.win'." in Problems, plus a notification "Integrity check passed."
- Update check in progress: "Checking for updates..." in the Updates page with a spinner (a static label when Reduce motion is on).
- Version detection while loading: the status item shows "Detecting version..." and then the value.

## 14. Parity coverage

| ID | Feature | Section |
| --- | --- | --- |
| UTMT-PLAT-02 | GUI platform | 3 |
| UTMT-PLAT-03 | In-app updater | 4 |
| UTMT-PLAT-04 | Scripts shipped beside the exe | 5 |
| UTMT-PLAT-05 | Multiple instances | 6 |
| UTMT-FMT-01 | Data file containers | 7.1 |
| UTMT-FMT-02 | Chunk coverage | 7.2 |
| UTMT-FMT-03 | Bytecode versions | 7.3 |
| UTMT-FMT-05 | Version detection | 7.4 |
| UTMT-FMT-06 | YYC games | 7.5 |
| UTMT-FMT-09 | Debug data format | 9.1 |
| UTMT-FMT-10 | Write integrity checks | 8 |
| UTMT-FMT-11 | Pointer offset map | 9.2 |
| UTMT-FILE-10 | Generate offset map | 9.2 |
| UTMT-FILE-05 | Unsupported bytecode handling | 7.3 |
| UTMT-FILE-06 | YYC detection | 7.5 |
| UTMT-FILE-11 | Audio group child instances | 6 |

UTMT-PLAT-01 (file associations) is in [onboarding.md](onboarding.md) section 7. UTMT-FMT-04 (GMS1 and GMS2 split), UTMT-FMT-07 (audio formats) and UTMT-FMT-08 (texture formats) are in [editors/rooms.md](editors/rooms.md), [editors/sounds.md](editors/sounds.md) and [editors/texture-pages.md](editors/texture-pages.md).
