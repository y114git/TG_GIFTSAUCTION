# Bypass audit, 2026-11

Principle: Studio never forbids something without an option to go on after a clear warning. The defaults protect most users; anyone can go on and take the risk.

Two switches in Settings > Advanced: `AppSettings.AskRiskyActions` (default on) and `AppSettings.AllowUnsafeSilently` (default off). `RiskPolicy.Of` turns them into Refuse (first one off), Ask (default) or Allow (second one on, no dialog). Every bypass is a normal edit: journaled and undoable until saved.

Decision key: A = protects the file or the project from damage that cannot be repaired, the guard stays and the message says why. B = bypass built. L = the refusal comes from G3MLib, a library limit (G3MLib is not changed here).

## Table

| Item | Where | What is refused | Decision | Bypass or reason |
|---|---|---|---|---|
| Delete a resource that code uses | `ClearReferencesCommand.cs` (CodeUseException), `MainWindow.Resources.cs` DeleteAsync, DeleteManyAsync, EditOrForceAsync | Delete stops when code names the resource | B | Warning "Delete {name} and leave the code that uses it?", then the delete runs and the code keeps its reference. Single and multi delete. A test writes and rereads the file after the forced delete. |
| New resource with an unusual name | `ResourceListCommand.cs` Create, CheckUnusualName; `MainWindow.Resources.cs` CreateResource | A name that is not letters, digits and underscores | B | The dialog warns once; pressing the button again (or Allow) creates it. |
| New resource name that is not a safe file name | `ResourceListCommand.cs` CheckUnusualName, `CopyPackage.IsSafeName` | Empty, over 200 characters, path separators, colon, dot names | A | Resources are stored as files in a project and in copy packages; such a name could leave the folder. The message says so. |
| Duplicate resource name | `ResourceListCommand.cs` CheckName | A second resource or code entry with an existing name | B | Warned once in the new-resource dialog, then created. The file stays readable; the editor and delete reach only the first of two equal names, and the warning says so. |
| Delete an object that is a collision partner | `ResourceListCommand.cs` DropCollisionsAimedAt | Events aim at the object by list position | B | After a warning the collision events that aim at it, and their code, are removed from the other objects (undo restores them). They cannot be left: the positions after the removed entry shift, and an event aimed at it would point at the next object or past the end of the list. |
| Rename refusals (reserved word, bad name, taken) | `EditCommands.cs` RenameResourceCommand, G3MLib `RenameService` | Rename stops | L | Raised inside G3MLib. |
| Code does not compile | `CodeEditorTab.cs` Apply, `EditCodeCommand` | Apply is refused, diagnostics shown | L, A | The text stays in the tab and is not lost. Bytecode cannot be made from text that does not compile, so there is nothing valid to write. Saving it raw and unverified would need an assemble-anyway call in G3MLib. The assembly view (`EditAssemblyCommand`) is the manual way. |
| Edit while the workspace saves | `Workspace.Edits.cs`, `EditHistory.cs:76` | Background edit (copy) was refused | B | Ordinary edits already queue while saving. A background edit now waits for the save to end and then runs. The guard in `EditHistory` (a direct Execute on a frozen history) stays: it is an internal check no UI path reaches. |
| Save or revert before a bulk import, script, patch, mod, repack; mod and patch export | `Workspace.Bulk.cs`, `MainWindow.Risky.cs` UnsavedOkAsync | Needs a clean state | B | Warned, then the change runs: the undo history is cleared, the unsaved edits stay in memory and crash recovery restarts from there. An export is made from the saved file, so the warning says the unsaved edits are not in it. |
| Replace audio over 64 MB, not WAV or OGG, kind change | `ReplaceAudioCommand.cs` | Size cap and type rules | B | Warned, then stored as given. Another kind keeps the sound's old type and flags (the game may not play it). An empty file stays refused. |
| Audio editor limits (64M samples, 8 and 16 bit PCM) | `AudioCodec.cs:35,76`, `WavEdit.cs:34` | The in-app editor will not open it | A | The editor decodes to floats in memory (256 MB at the limit); the audio itself can still be replaced from a file without this limit. |
| Copy package over 256 MB | `CopyPackage.cs` Zip | Copy to other file stops | B | Warned, then the copy runs again without the cap (the package is held in memory and the journal). |
| Copy with a different bytecode version or GameMaker Studio 1 | `CopyPlan.cs:76,101`, `CopyResourcesCommand.cs:63` | Every item refused | A | A different bytecode version gives a file the target cannot read. A target older than the source only warns today. |
| Replace a page picture with another size | `ReplacePageCommand.cs` | Size must match | B | Warned, then replaced; frames, glyphs and tiles keep their rectangles and may show the wrong part. |
| Font glyphs do not fit the texture | `FontReplaceCommand.Glyphs.cs:123` | Replace stops | A | Writing outside the texture damages the picture. The message names the fix (smaller size). |
| Remove the last frame of a sprite or the last point of a path | `FrameListCommand.cs`, `PathPointsCommand.cs` | Refused | B | Warned, then removed (the file still writes and reads); the game can fail where it uses them. |
| Index out of range (frames, glyphs, tracks, events, moments) | many commands, messages "has no ..." | Refused | A | Writing an index past the end makes the file unreadable. Internal guard, not a user rule. |
| "Edit was never applied" and not-found guards | Revert methods | Refused | A | State errors, nothing to bypass. |
| Room render over 16384 px | `RoomView.Render.cs` | PNG export stops | B | Warned, then the picture is drawn reduced so its longer side is 16384 px. |
| Property value that does not fit | `EditCommands.cs:276` | Value refused | L | Type conversion of the property model; the property keeps its type. |
| Unsupported import file type | `FileImport.cs` | Not a picture, sound or GML file | B | Warned, then the file is read as text and becomes a script. A binary file (a zero byte in the text) stays refused. |
| Plugin download over 50 MB | `PluginDownload.cs:8` | Download stops | A | Security cap on untrusted downloads. |
| Project busy or data file changed outside | `ProjectLink.cs:141,142` | Project sync stops | A | A sync during a change would write over unseen changes; the owner chooses how to bring them in. |
| Temp run warning | `MainWindow.Run.cs:112` | Not a refusal | none | Already a warning with a setting (`WarnTempRun`). |
| Close with unsaved changes | `ClosePromptWindow.cs` | Not a refusal | none | Already asks and offers Discard. |

## Left refused

Group A items keep their guard and say why:

- A resource name that is not a safe file name (empty, over 200 characters, slash, colon, dot names). The data file could hold it, but projects and copy packages store resources as files, and the project sync would fail or leave its folder.
- Copy with a different bytecode version or GameMaker Studio 1 against a later one: not proven safe; the library drops properties the target lacks, and the result was not tested on a real game.
- Font glyphs that do not fit the texture: drawing outside the picture damages it.
- Index guards and "was never applied" checks: internal state errors.
- Plugin download over 50 MB: cap on untrusted code, no UI path to overrule it yet.
- Project sync while busy or after an outside change: the owner already chooses how to bring changes in.

Group L items need G3MLib changes (rename refusals, code that does not compile, property value conversion). OI-341 lists what is done.
