# UTMT bundled scripts: analysis and decisions

Status: done 2026-10-26. Input: the 110 `.csx` scripts of UndertaleModTool 0.9.2.0 (revision f43e12c, read only copy), the compile and run results in [utmt-script-compat.md](utmt-script-compat.md) and the inventory in [utmt-parity.md](utmt-parity.md#bundled-script-inventory). Every script was read (header, prompts, main loop). Owner decision Q42: G3MStudio ships no bundled scripts; script concepts become built-in features. The result is in [../design/script-features.md](../design/script-features.md).

Reading the columns:

- Quality: good (works as intended, current API), outdated (works on old data or old GameMaker versions, or has known bugs), broken (does not run, or its author disabled it), niche (works, but only for a rare case or one game).
- Concept: yes when the idea is worth carrying over, even if the code is not.
- Decision: "Built-in SF-xx" the script defines the feature or a main part of it. "Merged into SF-xx" the script is a variant or duplicate of a feature that another script defines. "Plugin: ..." a game specific or optional patch, candidate for a later plugin (list in script-features.md, section 5). "Dropped" with the reason.

SF numbers are the features of script-features.md: SF-01 Export Resources, SF-02 Import Resources, SF-03 Search and Replace, SF-04 Rename by Pattern, SF-05 Find Resources, SF-06 Integrity Check and Repair, SF-07 Remove Unreferenced Resources, SF-08 Texture Page Tools, SF-09 Copy From Another Data File, SF-10 Audio Tools, SF-11 Asset Order and Room Order, SF-12 Compare Folders, SF-13 Data File Tools.

## Resource Exporters (18)

| Script | Folder | What it does | Quality | Concept | Decision |
| --- | --- | --- | --- | --- | --- |
| ExportAllAssembly | Resource Exporters | Writes the disassembly of every top level code entry to `.asm` files. | good | yes | Built-in SF-01 (code as assembly) |
| ExportAllCode | Resource Exporters | Decompiles every top level code entry to `.gml` in parallel, failures written into the file. | good | yes | Built-in SF-01 (code as GML) |
| ExportAllEmbeddedTextures | Resource Exporters | Saves each embedded texture page as `<index>.png`. | good | yes | Built-in SF-01 (texture pages) |
| ExportAllFonts | Resource Exporters | Writes each font atlas as PNG and its glyph table as semicolon CSV, without newer font fields. | outdated | yes | Built-in SF-01 (fonts with a full settings sidecar) |
| ExportAllMasks | Resource Exporters | Exports collision masks as black and white PNG, one per mask. | good | yes | Built-in SF-01 (masks) |
| ExportAllRoomsToPNG | Resource Exporters | Renders each room to PNG through the UTMT window (WPF), leaves trailing bytes when overwriting. | broken | yes | Built-in SF-01 (room images, rendered by the room editor renderer) |
| ExportAllShaders | Resource Exporters | Writes every shader stage as text and every binary blob as `.bin`, one folder per shader. | good | yes | Built-in SF-01 (shaders, round trip with SF-02) |
| ExportAllSounds | Resource Exporters | Exports embedded and external sounds as WAV or OGG, optionally per audio group; writes an empty file when audio is missing. | good | yes | Built-in SF-01 (sounds with settings sidecar) |
| ExportAllSprites | Resource Exporters | Exports sprite frames per texture page with padding and subfolder options and bounded memory. | good | yes | Built-in SF-01 (sprites, reference implementation) |
| ExportAllStrings | Resource Exporters | Writes the string table as text lines; the newline question is inverted. | broken | no | Merged into SF-01 (strings are JSON with index; plain text is an option) |
| ExportAllStringsJSON | Resource Exporters | Writes `{ "Strings": [...] }` in table order with a hand written escaper; breaks on an empty table. | good | yes | Built-in SF-01 (strings) |
| ExportAllTextures | Resource Exporters | Exports sprite, font and tileset frames into three subfolders, cropped. | outdated | no | Merged into SF-01 (layout: by type) |
| ExportAllTexturesGrouped | Resource Exporters | Same as above with a folder per asset. | outdated | no | Merged into SF-01 (layout: folder per asset) |
| ExportAllTilesets | Resource Exporters | Exports each tileset or background image as PNG, without tile size metadata. | good | yes | Built-in SF-01 (tilesets with settings sidecar) |
| ExportSpecificCode | Resource Exporters | Decompiles only the code of typed names (objects expand to their events); exact match, null on cancel. | good | yes | Merged into SF-01 (selection based export) |
| ExportSpecificSprites | Resource Exporters | Exports typed sprite names with optional padding. | good | yes | Merged into SF-01 (selection and padding option) |
| ExportSpritesAsGIF | Resource Exporters | Exports sprites as animated GIF using the sprite speed for the frame delay. | good | yes | Built-in SF-01 (sprite format GIF) |
| ExportTextureGroups | Resource Exporters | Exports pages, sprites, fonts and tilesets in a folder per texture group. | good | yes | Merged into SF-01 (layout: by texture group) |

## Resource Importers (17)

| Script | Folder | What it does | Quality | Concept | Decision |
| --- | --- | --- | --- | --- | --- |
| ApplyBasicGraphicsMod | Resource Importers | Swaps same size PNG frames into existing sprite slots with strict validation before any write. | good | yes | Built-in SF-02 (replace in place mode) |
| ImportAllEmbeddedTextures | Resource Importers | Replaces whole texture pages from `<index>.png`; no size check, one message per failure. | good | yes | Built-in SF-08 (replace pages, with size check) |
| ImportAllStrings | Resource Importers | Replaces the string table from a text file by line position; skips multi-line strings. | outdated | yes | Merged into SF-02 (strings, JSON with validation) |
| ImportAllStringsJSON | Resource Importers | Replaces strings by index from a `Strings` array; no length check, partial apply. | outdated | yes | Built-in SF-02 (strings) |
| ImportAllTilesets | Resource Importers | Replaces tileset images by file name. | good | yes | Built-in SF-02 (tilesets and backgrounds) |
| ImportAssembly | Resource Importers | Assembles `.asm` files into existing code entries, optionally stopping at the first error. | good | yes | Built-in SF-02 (assembly) |
| ImportFonts | Resource Importers | Packs font PNGs onto new pages and builds fonts from `glyphs_<name>.csv`; writes into the program folder. | outdated | yes | Built-in SF-02 (fonts) |
| ImportGML | Resource Importers | Compiles `.gml` files as one group, creating assets and links when possible. | good | yes | Built-in SF-02 (GML with diagnostics) |
| ImportGMS2FontData | Resource Importers | Imports a GMS2 `.yy` font and its PNG; needs Newtonsoft, tested on one IDE version only. | niche | yes | Merged into SF-02 (fonts from `.yy`) |
| ImportGraphics | Resource Importers | Packs `name_N.png` sprites and backgrounds from folders onto new pages with trim and mask rebuild. | good | yes | Built-in SF-02 (sprites and backgrounds) |
| ImportGraphicsAdvanced | Resource Importers | Adds strips, GIF, 0 or 1 based frames, single images and an origin and speed dialog (WinForms). | good | yes | Built-in SF-02 (strips, GIF, origin and speed) |
| ImportMasks | Resource Importers | Replaces collision masks from black and white PNG after a size check. | good | yes | Built-in SF-02 (masks) |
| ImportShaders | Resource Importers | Creates or updates shaders from the export layout; a partial folder wipes the stages it lacks. | good | yes | Built-in SF-02 (shaders) |
| ImportSingleSound | Resource Importers | Imports one WAV or OGG, embedded or external, with audio group creation. | good | yes | Merged into SF-02 (sounds; policy in SF-10) |
| ImportSounds | Resource Importers | Bulk sound import; state leaks from one file to the next and the group file is rewritten per file. | outdated | yes | Built-in SF-02 (sounds, one policy dialog) |
| NewTextureRepacker | Resource Importers | Deletes all pages and repacks sprites, fonts and backgrounds; loses items it does not own. | outdated | no | Merged into SF-08 (superseded by the reference keeping repack) |
| ReduceEmbeddedTexturePages | Resource Importers | Repacks small page items into fewer pages with size, padding, power of two and grouping options, keeping every reference. | good | yes | Built-in SF-08 (repack) |

## Sample Scripts (6)

| Script | Folder | What it does | Quality | Concept | Decision |
| --- | --- | --- | --- | --- | --- |
| DeltaHATE | Sample Scripts | Shuffles sprites, sounds, fonts and strings of old Deltarune builds with a seed and a power value. | niche | no | Plugin: Deltarune (randomizer, low priority) |
| DeltaMILK | Sample Scripts | Replaces every non background sprite frame with one texture. | niche | no | Dropped: single purpose joke |
| HeCanBeEverywhere | Sample Scripts | Adds a boss as a random overworld enemy with hard coded instruction edits. | niche | no | Dropped: build specific demo, not idempotent |
| MixMod | Sample Scripts | Disabled demo for a YouTube music extension; embeds an API key. | broken | no | Dropped: disabled by its author, external DLL |
| RoomOfDetermination | Sample Scripts | Builds a new room and objects in Undertale 1.08 as an API demo. | niche | no | Dropped: demo content for one version |
| TheWholeWorldRevolving | Sample Scripts | Appends GML that rotates the Deltarune view. | niche | no | Dropped: one line code append joke |

## Technical Scripts (35)

| Script | Folder | What it does | Quality | Concept | Decision |
| --- | --- | --- | --- | --- | --- |
| 13_To_14 | Technical Scripts | Upgrades bytecode 13 to 14; does not compile on G3MLib, detection is weak. | outdated | no | Dropped: pre-2015 files, unverified writer layouts |
| 14_To_16 | Technical Scripts | Rewrites variable ids and instance types of bytecode 14 files; its author calls it experimental. | broken | no | Dropped: broken and does not compile |
| 15_To_16 | Technical Scripts | Adds LANG and GLOB to bytecode 15 or removes TGIN from 17; blocks 2.3 files. | outdated | yes | Merged into SF-13 (convert bytecode version) |
| 15_to_17_To_16 | Technical Scripts | Draft merge of the two scripts above, forwards 2.3 files to another script. | outdated | no | Merged into SF-13 |
| 16_To_17 | Technical Scripts | Adds a TGIN chunk with one default texture group to bytecode 16. | niche | yes | Merged into SF-13 |
| AutoBackup | Technical Scripts | Timed copies of the open data file with a path bug and a prompt every few backups. | outdated | yes | Dropped: replaced by backups and local history (shell 14.2, ADR-0010) |
| CheckDecompiler | Technical Scripts | Decompiles, recompiles and compares every code entry and writes failure lists. | good | yes | Built-in SF-06 (decompile round trip check) |
| ConvertFrom17to16 | Technical Scripts | Downgrades bytecode 17 or higher to 16 by setting the version and removing TGIN. | niche | yes | Built-in SF-13 (convert bytecode version) |
| ConvertFrom17to16_for_2.3 | Technical Scripts | Experimental downgrade of 2.3 files to the 2.0 format; needs an unshipped script. | broken | no | Dropped: needs a code translator, author says unstable |
| CopySound | Technical Scripts | Copies named sounds from another file including audio group files and external OGG. | outdated | yes | Merged into SF-09 (sounds) |
| CopySoundInternal | Technical Scripts | Duplicates named sounds inside the file as `_Copy`; no name check. | outdated | yes | Merged into SF-09 (duplicate) |
| CopySpriteBgFont | Technical Scripts | Copies sprites, tilesets and fonts with page items from another file. | outdated | yes | Built-in SF-09 (sprites, tilesets, fonts) |
| CopySpriteBgFontInternal | Technical Scripts | Duplicates sprites, tilesets and fonts inside the file by copying every page. | outdated | yes | Merged into SF-09 (duplicate) |
| ExecutionOrder | Technical Scripts | Instruments every object event so an overlay shows run order and one global flag use. | niche | yes | Plugin: any game (event trace) |
| ExportAllCodeSync | Technical Scripts | Single threaded code export; same output as ExportAllCode. | good | no | Merged into SF-01 (code, failure folder convention) |
| ExportAssetOrder | Technical Scripts | Writes all asset names per type in index order to a text file. | good | yes | Built-in SF-11 (export order) |
| FindNullsInOffsetMap | Technical Scripts | Maps error log offsets to the objects around them in a raw data file. | niche | no | Dropped: only helps debug the UTMT reader (`tools.offsetMap` exists) |
| FindUnknownFunctions | Technical Scripts | Lists functions nothing defines and can delete them without checking references. | niche | yes | Merged into SF-06 (report, repair only after a reference check) |
| FindUnusedStrings | Technical Scripts | Counts references to every string and removes unused ones; also deletes variables and functions in a report run. | outdated | yes | Built-in SF-07 (strings, variables, functions) |
| GameObjectCopy | Technical Scripts | Copies objects from another file; recompiles code through the decompiler, hides errors. | outdated | yes | Merged into SF-09 (objects) |
| GameObjectCopyInternal | Technical Scripts | Duplicates objects as `_Copy` and forgets the code locals entry. | outdated | yes | Merged into SF-09 (duplicate) |
| GetAllChunkNames | Technical Scripts | Prints the chunk names of the file as a C# array. | niche | no | Dropped: developer helper |
| GetTempDirectory | Technical Scripts | Returns the UTMT base directory. | niche | no | Dropped: debugs the UTMT bundled runtime |
| ImportAssetOrder | Technical Scripts | Reorders asset lists from an exported order file; warns on 2024.11 and newer. | outdated | yes | Merged into SF-11 (import order) |
| ImportGraphics_Full_Repack | Technical Scripts | Imports PNGs and repacks all pages; clears all pages before packing. | outdated | yes | Merged into SF-08 (repack) |
| LintAllScripts | Technical Scripts | Compiles every bundled script and lists the failures. | good | no | Dropped: no scripts are bundled (`scripts.lint` checks any script) |
| MatchRoomOrderToInternalOrder | Technical Scripts | Reorders room assets to follow the room order list, parsing text. | outdated | yes | Merged into SF-11 (room order) |
| Profiler | Technical Scripts | Instruments events to write time per object, call counts and stack traces to disk. | niche | yes | Plugin: any game (event trace and profiler) |
| RealignRoomInternalOrder | Technical Scripts | Rebuilds the room order list from the asset order. | good | yes | Merged into SF-11 (room order) |
| RemoveUnusedSounds | Technical Scripts | Removes embedded audio no sound uses and unused audio group files; does not renumber audio ids. | outdated | yes | Merged into SF-07 (audio data), group handling in SF-10 |
| RestoreMissingCodeLocals | Technical Scripts | Adds a minimal code locals entry to entries that have none. | good | yes | Merged into SF-06 (repair) |
| SpriteOriginCopy | Technical Scripts | Copies origins of same named sprites from another file. | good | yes | Merged into SF-09 (properties mode) |
| TestExportAllCode | Technical Scripts | Exports all code with a crash resume status file and assembly for failures. | niche | yes | Merged into SF-01 (code failure list, assembly fallback) |
| ToggleAlignment | Technical Scripts | Toggles 4 byte alignment of texture page items on save. | good | yes | Merged into SF-13 (file settings) |
| VariableFixer | Technical Scripts | Renumbers variable ids and header counters; also switches the debugger off without saying so. | good | yes | Merged into SF-06 (repair, counters recomputed on save) |

## UTDR Scripts (24)

All 24 target Undertale or Deltarune. The folders `Borders` (23 PNG) and `TouchControls_data` (1 PNG, 4 GML files) belong to the border and touch control plugin candidates.

| Script | Folder | What it does | Quality | Concept | Decision |
| --- | --- | --- | --- | --- | --- |
| Debug | UTDR Scripts | Turns the debug flag on or off in all known Undertale and Deltarune builds. | good | yes | Plugin: Undertale and Deltarune (flag toggle idea in P-1) |
| DeltaruneClearShaderData | UTDR Scripts | Empties all shaders and blanks palette swap scripts for old graphics hardware. | broken | no | Dropped: calls a function UTMT does not define |
| DeltaruneReloadJSON | UTDR Scripts | Binds a key that reloads the language JSON in an old Deltarune build. | outdated | no | Plugin: Deltarune (with P-4) |
| DeltaruneTTFFonts | UTDR Scripts | Removes all fonts and loads TTF files at start with a hard coded list. | outdated | yes | Plugin: Deltarune (generic form is P-3) |
| DisableDogcheck | UTDR Scripts | Removes the dog check from the load script so any room loads. | good | yes | Plugin: Undertale and Deltarune |
| ShowRoomName | UTDR Scripts | Draws room id and name under the debug timer. | good | yes | Plugin: Undertale and Deltarune (generic form is P-1) |
| TouchControlsEnabler | UTDR Scripts | Adds a joystick, buttons and a settings screen with its own texture, sprites and object. | niche | yes | Plugin: Undertale and Deltarune (P-7) |
| UndertaleBattlegroupSelector | UTDR Scripts | Adds a debug number entry that starts any battle group. | niche | no | Plugin: Undertale |
| UndertaleBetterVaporiser | UTDR Scripts | Replaces the vaporize effect with per pixel particles. | niche | no | Plugin: Undertale (low priority) |
| UndertaleBorderEnabler | UTDR Scripts | Enables 1920x1080 borders on old PC versions with about 15 patches and new pages. | good | yes | Plugin: Undertale (P-5) |
| UndertaleBorderEnablerv1_11 | UTDR Scripts | Same for version 1.11 with strict patches. | good | yes | Plugin: Undertale (P-5) |
| UndertaleChangeHomeBattlegroup | UTDR Scripts | Changes the battle group started by the Home key. | niche | no | Plugin: Undertale (with the battle group selector) |
| UndertaleDebugMsg | UTDR Scripts | Draws a message array on screen in debug; uses a removed function. | outdated | no | Dropped: Undertale text format, dead function |
| UndertaleDebugToggler | UTDR Scripts | Binds F1 to toggle the debug flag. | good | yes | Plugin: Undertale (idea in P-1) |
| UndertaleDialogSimulator | UTDR Scripts | Adds a room and about 700 lines of GML that preview text box syntax. | good | yes | Plugin: Undertale |
| UndertaleFixAlphysLabCrashAndroid | UTDR Scripts | Empties a darkness object and one OS check for an Android crash. | niche | no | Dropped: Android workaround |
| UndertaleGoToRoom | UTDR Scripts | Binds F3 to a go to room number dialog. | good | yes | Plugin: any game (P-1) |
| UndertaleRunButton | UTDR Scripts | Lets the player run outside debug mode. | good | no | Plugin: Undertale |
| UndertaleSafeBlaster | UTDR Scripts | Adds a boss attack object not tied to the boss fight. | good | no | Dropped: modder helper (object copy is SF-09) |
| UndertaleSimplifyBattlegroupScript | UTDR Scripts | Moves default lines of one script into a prepended block. | niche | no | Dropped: modder preparation step (SF-03 covers such edits) |
| UndertaleSwitchAndXboxOnPC | UTDR Scripts | Makes the Switch or Xbox build run on PC with about 35 strict patches. | good | yes | Plugin: Undertale |
| UndertaleTTFFonts | UTDR Scripts | Same as the Deltarune font script; its game guard has no return. | outdated | yes | Plugin: any game (P-3) |
| UndertaleWASD | UTDR Scripts | Adds WASD by simulating arrow key presses. | good | no | Plugin: Undertale |
| UndertaleWithJSONs | UTDR Scripts | Moves all Undertale text into JSON files with a language switch; needs Newtonsoft. | good | yes | Plugin: Undertale (P-4) |

## Utility Scripts (10)

| Script | Folder | What it does | Quality | Concept | Decision |
| --- | --- | --- | --- | --- | --- |
| ExternalizeAllOGGs | Utility Scripts | Exports embedded sounds and rewrites entries as external files; renames WAV bytes to `.ogg`, rewrites group files without backup. | outdated | yes | Built-in SF-10 (externalize, embed, convert) |
| ExtractEmbeddedDataFile | Utility Scripts | Scans an executable or dump for a `FORM` and `GEN8` header and writes the data file; no end of file guard. | good | yes | Built-in SF-13 (extract from executable) |
| FancyRoomSelect | Utility Scripts | Injects an in-game room picker with search, opened with F3, into any game. | good | yes | Plugin: any game (P-1) |
| FindAndReplace | Utility Scripts | Find and replace across all GML through one import group, no preview. | good | yes | Built-in SF-03 |
| FindObjectsInRooms | Utility Scripts | Lists room instances of typed object names. | good | yes | Built-in SF-05 (in rooms) |
| FindObjectsWithSprite | Utility Scripts | Lists objects whose sprite matches names or a regex; ignores the case flag in plain mode. | good | yes | Merged into SF-05 (by sprite) |
| FontEditor | Utility Scripts | WinForms glyph editor with pixel painting, size, shift and offset edits, and ZIP import and export. | outdated | yes | Merged into the Fonts editor (list in script-features.md, section 6) |
| GoToRoom_AutoLocatePersistentObj | Utility Scripts | Replaces the F3 handler of the first persistent object with a go to room dialog. | good | yes | Plugin: any game (P-1) |
| MergeImages | Utility Scripts | Writes side by side images of same named PNG files from two folders. | niche | yes | Built-in SF-12 |
| ScaleAllTextures | Utility Scripts | Scales every texture page and remaps all item, glyph and tile coordinates; can overflow 16 bit values. | niche | yes | Built-in SF-08 (scale pages) |

## Decisions grouped

| Decision | Count | Scripts |
| --- | ---: | --- |
| Built-in feature | 36 | SF-01: 12 (ExportAllAssembly, ExportAllCode, ExportAllEmbeddedTextures, ExportAllFonts, ExportAllMasks, ExportAllRoomsToPNG, ExportAllShaders, ExportAllSounds, ExportAllSprites, ExportAllStringsJSON, ExportAllTilesets, ExportSpritesAsGIF). SF-02: 11 (ApplyBasicGraphicsMod, ImportAllStringsJSON, ImportAllTilesets, ImportAssembly, ImportFonts, ImportGML, ImportGraphics, ImportGraphicsAdvanced, ImportMasks, ImportShaders, ImportSounds). SF-03: 1 (FindAndReplace). SF-05: 1 (FindObjectsInRooms). SF-06: 1 (CheckDecompiler). SF-07: 1 (FindUnusedStrings). SF-08: 3 (ImportAllEmbeddedTextures, ReduceEmbeddedTexturePages, ScaleAllTextures). SF-09: 1 (CopySpriteBgFont). SF-10: 1 (ExternalizeAllOGGs). SF-11: 1 (ExportAssetOrder). SF-12: 1 (MergeImages). SF-13: 2 (ConvertFrom17to16, ExtractEmbeddedDataFile). |
| Merged into a feature | 32 | SF-01: 8 (ExportAllStrings, ExportAllTextures, ExportAllTexturesGrouped, ExportSpecificCode, ExportSpecificSprites, ExportTextureGroups, ExportAllCodeSync, TestExportAllCode). SF-02: 3 (ImportAllStrings, ImportGMS2FontData, ImportSingleSound). SF-05: 1 (FindObjectsWithSprite). SF-06: 3 (FindUnknownFunctions, RestoreMissingCodeLocals, VariableFixer). SF-07: 1 (RemoveUnusedSounds). SF-08: 2 (NewTextureRepacker, ImportGraphics_Full_Repack). SF-09: 6 (CopySound, CopySoundInternal, CopySpriteBgFontInternal, GameObjectCopy, GameObjectCopyInternal, SpriteOriginCopy). SF-11: 3 (ImportAssetOrder, MatchRoomOrderToInternalOrder, RealignRoomInternalOrder). SF-13: 4 (15_To_16, 15_to_17_To_16, 16_To_17, ToggleAlignment). Fonts editor: 1 (FontEditor). |
| Plugin candidate | 24 | Any game: ExecutionOrder, Profiler, FancyRoomSelect, GoToRoom_AutoLocatePersistentObj, UndertaleGoToRoom, UndertaleTTFFonts (6). Undertale or Deltarune: 17 UTDR scripts (Debug, DeltaruneReloadJSON, DeltaruneTTFFonts, DisableDogcheck, ShowRoomName, TouchControlsEnabler, UndertaleBattlegroupSelector, UndertaleBetterVaporiser, UndertaleBorderEnabler, UndertaleBorderEnablerv1_11, UndertaleChangeHomeBattlegroup, UndertaleDebugToggler, UndertaleDialogSimulator, UndertaleRunButton, UndertaleSwitchAndXboxOnPC, UndertaleWASD, UndertaleWithJSONs) and DeltaHATE. |
| Dropped | 18 | DeltaMILK, HeCanBeEverywhere, MixMod, RoomOfDetermination, TheWholeWorldRevolving, 13_To_14, 14_To_16, ConvertFrom17to16_for_2.3, AutoBackup, FindNullsInOffsetMap, GetAllChunkNames, GetTempDirectory, LintAllScripts, DeltaruneClearShaderData, UndertaleDebugMsg, UndertaleFixAlphysLabCrashAndroid, UndertaleSafeBlaster, UndertaleSimplifyBattlegroupScript |
| Total | 110 | 36 + 32 + 24 + 18 |

Dropped reasons in one line each: joke or demo content (5), pre-2017 bytecode converters that need unverified writers (3), replaced by an existing G3MStudio feature (AutoBackup, LintAllScripts, FindNullsInOffsetMap), developer helpers for the UTMT runtime (2), broken in UTMT itself (DeltaruneClearShaderData), Undertale only modder helpers (3).

## Findings that shape the features

1. Seven exporters and six importers differ only in layout, padding and selection. One export and one import with options replace them (SF-01, SF-02).
2. Every script overwrites silently, has no preview, no undo and no summary. All built-in features show a plan first, take a checkpoint, report per item and are one undo step.
3. Sidecar data is missing everywhere (sprite origin and speed, sound volume and group, font kerning, tileset tile size). Without it an export cannot round trip. SF-01 writes one `settings.json` next to the files and SF-02 reads it.
4. Five copies of a texture packer exist in the importers. One packer (SF-08) serves import, repack and font import.
5. Name list dialogs (typed names, exact match, null on cancel) become a checklist with a pattern filter and an unmatched names report.
6. Known script bugs not to repeat: inverted newline question (ExportAllStrings), `File.OpenWrite` without truncation (ExportAllRoomsToPNG), `audiogroup` files rewritten without backup, state leak between files (ImportSounds), audio ids not renumbered (RemoveUnusedSounds), read only run that mutates data (FindUnusedStrings), pages cleared before packing (ImportGraphics_Full_Repack), case flag ignored (FindObjectsWithSprite), no end of file guard (ExtractEmbeddedDataFile), 16 bit overflow (ScaleAllTextures).
7. 19 of the 24 UTDR scripts are one pattern: a code patch (replace, append, prepend, find and replace, regex find and replace) with a game check. A plugin API with those verbs, a strict "no match is an error" default and an "already applied" marker covers them (open item OI-053).
8. G3MLib gaps found by the analysis are collected per feature in script-features.md (column "G3MLib primitive") and in section 7.
