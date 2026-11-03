# UTMT bundled script compatibility

Run of the 110 `.csx` scripts that ship with UndertaleModTool (revision f43e12c) against the G3MLib script host (`ScriptGlobals` and `ScriptRunner`, with the UTMT name translator applied). Source: `UndertaleModTool/Scripts`, read only.

## Result

| Measure | Count |
| --- | ---: |
| Scripts | 110 |
| Compile | 98 |
| Do not compile | 12 |
| Run to completion on the fixture | 17 |
| Need a dialog (question, prompt, input), not run further | 57 |
| Compile but fail on the fixture | 24 |

Of the 98 scripts that compile, 17 ran to completion, 57 stopped at their first dialog and 24 failed. Every one of the 24 failures comes from the fixture, not the host: the scripts check for Undertale or Deltarune (game or version guards, 11 scripts) or reach for game resources the fixture lacks, such as `obj_time`, `spr_checkers_milk` or `gml_Object_obj_mainchara_Step_0` (13 scripts). None fails on a missing host member.

## Method

- Tool: `G3MLib.ScriptCompat` in the G3MLib repository, outside the shipped library. Command: `dotnet run --project G3MLib.ScriptCompat -c Release -- <Scripts folder> <data file> <work folder> <report.md>`.
- Compile: every script goes through `ScriptRunner.Compile`, which translates UTMT names and fails on any compiler error.
- Run: each script that compiles runs in its own work folder on a fresh copy of the fixture, `ExePath` and the script root pointing at a copy of the `Scripts` tree so nested `RunUMTScript` calls resolve. A host subclass answers no dialog: the first call to a question, input or prompt member ends the script and marks it "needs dialog". Timeout is 60 s per script, none hit it.
- Fixture: a 29 KB data file built with GameMaker LTS 2026 that holds no Undertale or Deltarune content. Scripts that patch game code cannot succeed on it, so the "run" column proves the host runs a script, not that the script's edit is right.

## Compile failures

| Script | Cause |
| --- | --- |
| ExportAllRoomsToPNG, ImportGraphicsAdvanced, FontEditor | WPF and WinForms types (`System.Windows`, `System.Drawing.Imaging`). The scripts drive the UTMT window, no cross-platform host can run them. |
| ImportGMS2FontData, UndertaleWithJSONs | Use Newtonsoft.Json, which G3MLib does not reference. |
| DeltaHATE | Reads the UTMT-internal field `GameObject._sprite`. |
| 13_To_14, ScaleAllTextures | A model member is `uint` here where UTMT has `int` (assignment without a cast). |
| 14_To_16 | `Instruction.Destination` is missing from the instruction model. |
| FindUnusedStrings | `Sequence.Keyframe.KeyframeChannel.Event` is missing. |
| RemoveUnusedSounds | `GameMakerReader.WarningHandlerDelegate` takes a different argument list. |
| DeltaruneClearShaderData | Calls `ImportASMString`, which is not defined in UTMT either. |

The model differences (`uint` against `int`, `Destination`, `KeyframeChannel.Event`, `WarningHandlerDelegate`) are candidates for alias members if script compatibility matters; none was changed here.

## Per script

| Script | Category | Compiles | Runs | Reason |
| --- | --- | --- | --- | --- |
| ExportAllAssembly.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ExportAllCode.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ExportAllEmbeddedTextures.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ExportAllFonts.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ExportAllMasks.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ExportAllRoomsToPNG.csx | Resource Exporters | no | - | compile: CS0234 The type or namespace name 'Media' does not exist in the namespace 'System.Windows' (are you missing an assembly reference?) (line 9); CS0234 The type or namespace name 'Media' does not exis... |
| ExportAllShaders.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ExportAllSounds.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ExportAllSprites.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ExportAllStrings.csx | Resource Exporters | yes | needs dialog | PromptSaveFile |
| ExportAllStringsJSON.csx | Resource Exporters | yes | needs dialog | PromptSaveFile |
| ExportAllTextures.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ExportAllTexturesGrouped.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ExportAllTilesets.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ExportSpecificCode.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ExportSpecificSprites.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ExportSpritesAsGIF.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ExportTextureGroups.csx | Resource Exporters | yes | needs dialog | PromptChooseDirectory |
| ApplyBasicGraphicsMod.csx | Resource Importers | yes | needs dialog | PromptChooseDirectory |
| ImportAllEmbeddedTextures.csx | Resource Importers | yes | needs dialog | PromptChooseDirectory |
| ImportAllStrings.csx | Resource Importers | yes | needs dialog | PromptLoadFile |
| ImportAllStringsJSON.csx | Resource Importers | yes | needs dialog | PromptLoadFile |
| ImportAllTilesets.csx | Resource Importers | yes | needs dialog | PromptChooseDirectory |
| ImportAssembly.csx | Resource Importers | yes | needs dialog | PromptChooseDirectory |
| ImportFonts.csx | Resource Importers | yes | needs dialog | PromptChooseDirectory |
| ImportGML.csx | Resource Importers | yes | needs dialog | PromptChooseDirectory |
| ImportGMS2FontData.csx | Resource Importers | no | - | compile: CS0246 The type or namespace name 'Newtonsoft' could not be found (are you missing a using directive or an assembly reference?) (line 11); CS0246 The type or namespace name 'Newtonsoft' could not b... |
| ImportGraphics.csx | Resource Importers | yes | needs dialog | ScriptQuestion |
| ImportGraphicsAdvanced.csx | Resource Importers | no | - | compile: CS0234 The type or namespace name 'Forms' does not exist in the namespace 'System.Windows' (are you missing an assembly reference?) (line 30); CS0246 The type or namespace name 'Form' could not be ... |
| ImportMasks.csx | Resource Importers | yes | needs dialog | PromptChooseDirectory |
| ImportShaders.csx | Resource Importers | yes | needs dialog | PromptChooseDirectory |
| ImportSingleSound.csx | Resource Importers | yes | needs dialog | PromptLoadFile |
| ImportSounds.csx | Resource Importers | yes | needs dialog | PromptChooseDirectory |
| NewTextureRepacker.csx | Resource Importers | yes | yes |  |
| ReduceEmbeddedTexturePages.csx | Resource Importers | yes | yes |  |
| DeltaHATE.csx | Sample Scripts | no | - | compile: CS1061 'GameMakerGameObject' does not contain a definition for '_sprite' and no accessible extension method '_sprite' accepting a first argument of type 'GameMakerGameObject' could be found (are yo... |
| DeltaMILK.csx | Sample Scripts | yes | no | run: System.NullReferenceException at  Line 5 in script DeltaMILK.csx: var milk = Data.Sprites.ByName("spr_checkers_milk").Textures[0].Texture; |
| HeCanBeEverywhere.csx | Sample Scripts | yes | no | run: System.NullReferenceException at  Line 28 in script HeCanBeEverywhere.csx: var spr_joker_enemy = new UndertaleSprite() |
| MixMod.csx | Sample Scripts | yes | no | run: This script has not been updated to reflect the new extension format in UndertaleModTool and so has been temporarily disabled. |
| RoomOfDetermination.csx | Sample Scripts | yes | no | run: System.NullReferenceException at  Line 46 in script RoomOfDetermination.csx: room_ruins1.GameObjects.ByInstanceID(100085).X = 20; |
| TheWholeWorldRevolving.csx | Sample Scripts | yes | no | run: System.NullReferenceException at  Line 20 in script TheWholeWorldRevolving.csx: var gml_Object_obj_time_Step_1 = Data.GameObjects.ByName("obj_time").EventHandlerFor(EventType.Step, EventSubtypeStep... |
| 13_To_14.csx | Technical Scripts | no | - | compile: CS0266 Cannot implicitly convert type 'uint' to 'int'. An explicit conversion exists (are you missing a cast?) (line 64) |
| 14_To_16.csx | Technical Scripts | no | - | compile: CS1061 'GameMakerInstruction' does not contain a definition for 'Destination' and no accessible extension method 'Destination' accepting a first argument of type 'GameMakerInstruction' could be fou... |
| 15_To_16.csx | Technical Scripts | yes | no | run: TESTis GMS 2.3+ and is ineligible |
| 15_to_17_To_16.csx | Technical Scripts | yes | needs dialog | ScriptQuestion |
| 16_To_17.csx | Technical Scripts | yes | yes |  |
| AutoBackup.csx | Technical Scripts | yes | needs dialog | ScriptQuestion |
| CheckDecompiler.csx | Technical Scripts | yes | yes |  |
| ConvertFrom17to16.csx | Technical Scripts | yes | needs dialog | ScriptQuestion |
| ConvertFrom17to16_for_2.3.csx | Technical Scripts | yes | needs dialog | ScriptQuestion |
| CopySound.csx | Technical Scripts | yes | needs dialog | PromptLoadFile |
| CopySoundInternal.csx | Technical Scripts | yes | needs dialog | SimpleTextInput |
| CopySpriteBgFont.csx | Technical Scripts | yes | needs dialog | PromptLoadFile |
| CopySpriteBgFontInternal.csx | Technical Scripts | yes | needs dialog | SimpleTextInput |
| ExecutionOrder.csx | Technical Scripts | yes | yes |  |
| ExportAllCodeSync.csx | Technical Scripts | yes | yes |  |
| ExportAssetOrder.csx | Technical Scripts | yes | needs dialog | PromptSaveFile |
| FindNullsInOffsetMap.csx | Technical Scripts | yes | needs dialog | PromptLoadFile |
| FindUnknownFunctions.csx | Technical Scripts | yes | needs dialog | PromptChooseDirectory |
| FindUnusedStrings.csx | Technical Scripts | no | - | compile: CS1061 'GameMakerSequence.Keyframe<GameMakerSequence.Moment>.KeyframeChannel' does not contain a definition for 'Event' and no accessible extension method 'Event' accepting a first argument of type... |
| GameObjectCopy.csx | Technical Scripts | yes | needs dialog | PromptLoadFile |
| GameObjectCopyInternal.csx | Technical Scripts | yes | needs dialog | SimpleTextInput |
| GetAllChunkNames.csx | Technical Scripts | yes | yes |  |
| GetTempDirectory.csx | Technical Scripts | yes | yes |  |
| ImportAssetOrder.csx | Technical Scripts | yes | needs dialog | PromptLoadFile |
| ImportGraphics_Full_Repack.csx | Technical Scripts | yes | needs dialog | ScriptQuestion |
| LintAllScripts.csx | Technical Scripts | yes | yes |  |
| MatchRoomOrderToInternalOrder.csx | Technical Scripts | yes | yes |  |
| Profiler.csx | Technical Scripts | yes | needs dialog | ScriptQuestion |
| RealignRoomInternalOrder.csx | Technical Scripts | yes | yes |  |
| RemoveUnusedSounds.csx | Technical Scripts | no | - | compile: CS1593 Delegate 'GameMakerReader.WarningHandlerDelegate' does not take 1 arguments (line 192) |
| RestoreMissingCodeLocals.csx | Technical Scripts | yes | yes |  |
| SpriteOriginCopy.csx | Technical Scripts | yes | needs dialog | PromptLoadFile |
| TestExportAllCode.csx | Technical Scripts | yes | yes |  |
| ToggleAlignment.csx | Technical Scripts | yes | needs dialog | ScriptQuestion |
| VariableFixer.csx | Technical Scripts | yes | yes |  |
| Debug.csx | UTDR Scripts | yes | needs dialog | ScriptQuestion |
| DeltaruneClearShaderData.csx | UTDR Scripts | no | - | compile: CS0103 The name 'ImportASMString' does not exist in the current context (line 28); CS0103 The name 'ImportASMString' does not exist in the current context (line 29) |
| DeltaruneReloadJSON.csx | UTDR Scripts | yes | no | run: System.NullReferenceException at  Line 16 in script DeltaruneReloadJSON.csx: var code = Data.GameObjects.ByName("obj_time").EventHandlerFor(EventType.KeyPress, EventSubtypeKey.vk_f12, Data); |
| DeltaruneTTFFonts.csx | UTDR Scripts | yes | no | run: System.NullReferenceException at  Line 27 in script DeltaruneTTFFonts.csx: importGroup.QueueAppend(obj_time.EventHandlerFor(EventType.Create, Data), @" |
| DisableDogcheck.csx | UTDR Scripts | yes | no | run: This script can only be used with Undertale or Deltarune Chapter 1. |
| ShowRoomName.csx | UTDR Scripts | yes | no | run: System.NullReferenceException at  Line 12 in script ShowRoomName.csx: var gml_Object_obj_time_Draw_64 = Data.GameObjects.ByName("obj_time").EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawGUI,... |
| TouchControlsEnabler.csx | UTDR Scripts | yes | needs dialog | ScriptQuestion |
| UndertaleBattlegroupSelector.csx | UTDR Scripts | yes | no | run: This script can only be used on UNDERTALE. |
| UndertaleBetterVaporiser.csx | UTDR Scripts | yes | no | run: This script only works with Undertale! |
| UndertaleBorderEnabler.csx | UTDR Scripts | yes | yes |  |
| UndertaleBorderEnablerv1_11.csx | UTDR Scripts | yes | no | run: System.InvalidOperationException at  Line 69 in script UndertaleBorderEnablerv1_11.csx: importGroup.Import(); |
| UndertaleChangeHomeBattlegroup.csx | UTDR Scripts | yes | no | run: Cannot apply, "gml_Object_obj_mainchara_KeyPress_36" does not exist! |
| UndertaleDebugMsg.csx | UTDR Scripts | yes | no | run: System.NullReferenceException at  Line 4 in script UndertaleDebugMsg.csx: var code = Data.GameObjects.ByName("obj_time").EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawGUI, Data); |
| UndertaleDebugToggler.csx | UTDR Scripts | yes | no | run: Unsupported game version. |
| UndertaleDialogSimulator.csx | UTDR Scripts | yes | no | run: This script can only be used with Undertale. |
| UndertaleFixAlphysLabCrashAndroid.csx | UTDR Scripts | yes | needs dialog | ScriptQuestion |
| UndertaleGoToRoom.csx | UTDR Scripts | yes | no | run: System.NullReferenceException at  Line 7 in script UndertaleGoToRoom.csx: var code = Data.GameObjects.ByName("obj_time").EventHandlerFor(EventType.KeyPress, EventSubtypeKey.vk_f3, Data); |
| UndertaleRunButton.csx | UTDR Scripts | yes | no | run: System.InvalidOperationException at  Line 29 in script UndertaleRunButton.csx: importGroup.Import(); |
| UndertaleSafeBlaster.csx | UTDR Scripts | yes | no | run: System.NullReferenceException at  Line 231 in script UndertaleSafeBlaster.csx: UndertaleCode codeToDecompile = obj_gasterbl_gen.EventHandlerFor(EventType.Alarm, (uint)0, Data); |
| UndertaleSimplifyBattlegroupScript.csx | UTDR Scripts | yes | no | run: System.InvalidOperationException at  Line 38 in script UndertaleSimplifyBattlegroupScript.csx: importGroup.Import(); |
| UndertaleSwitchAndXboxOnPC.csx | UTDR Scripts | yes | no | run: This script can only be used with the Nintendo Switch  and Xbox One version of Undertale. |
| UndertaleTTFFonts.csx | UTDR Scripts | yes | no | run: Error 1: This script only works with Undertale! |
| UndertaleWASD.csx | UTDR Scripts | yes | no | run: This script can only be used with Undertale! |
| UndertaleWithJSONs.csx | UTDR Scripts | no | - | compile: CS0246 The type or namespace name 'Newtonsoft' could not be found (are you missing a using directive or an assembly reference?) (line 6); CS0103 The name 'JsonConvert' does not exist in the current... |
| ExternalizeAllOGGs.csx | Utility Scripts | yes | needs dialog | ScriptQuestion |
| ExtractEmbeddedDataFile.csx | Utility Scripts | yes | needs dialog | PromptLoadFile |
| FancyRoomSelect.csx | Utility Scripts | yes | yes |  |
| FindAndReplace.csx | Utility Scripts | yes | needs dialog | ScriptQuestion |
| FindObjectsInRooms.csx | Utility Scripts | yes | needs dialog | SimpleTextInput |
| FindObjectsWithSprite.csx | Utility Scripts | yes | needs dialog | ScriptQuestion |
| FontEditor.csx | Utility Scripts | no | - | compile: CS0234 The type or namespace name 'Imaging' does not exist in the namespace 'System.Drawing' (are you missing an assembly reference?) (line 5); CS0234 The type or namespace name 'Forms' does not ex... |
| GoToRoom_AutoLocatePersistentObj.csx | Utility Scripts | yes | yes |  |
| MergeImages.csx | Utility Scripts | yes | needs dialog | PromptChooseDirectory |
| ScaleAllTextures.csx | Utility Scripts | no | - | compile: CS0266 Cannot implicitly convert type 'uint' to 'int'. An explicit conversion exists (are you missing a cast?) (line 123); CS0266 Cannot implicitly convert type 'uint' to 'int'. An explicit convers... |
