# GameMaker data file format coverage: G3MLib 1.0.1 against UndertaleModLib

Research date 2026-10-25. Read-only comparison; no source was edited. Phase L2 input (G3MLib 1.1.0 gap list).

## Scope and method

- G3MLib is a close derivative of UndertaleModLib (UTMT). Class names map one to one (`Undertale*` to `GameMaker*`), so every file pair was compared after renaming. Most differences are bounds checks, null handling and API cleanup. The format logic (fields, version gates, version detection) is nearly identical, so the real gaps are few and are listed in section 4.
- Paths. G3MLib paths are relative to the folder holding the G3M repositories, and in the tables they are shortened to be relative to `G3MLib/G3MLib/DataFile/`. UTMT paths are relative to the root of the read-only UndertaleModLib checkout, for example `UndertaleModLib/UndertaleChunks.cs`.
- UTMT flag names. This UTMT checkout has no version flag enum (no `GMS2_3`, `GM_2022_x` constants). Gates are numeric calls: `IsVersionAtLeast(major, minor, release, build)` and `IsNonLTSVersionAtLeast(...)`. The tables write them as tuples and name the GameMaker version they map to. `IsGameMaker2()` means major 2 or higher. The only enum named like a flag is `Flags.IsGMS2_3` in room data (`UndertaleModLib/Models/UndertaleRoom.cs` line 46), a room flag and not a version gate.
- Evidence. Besides reading code, a throwaway console program read and wrote files through the built G3MLib 1.0.1 Release library (Undertale, Deltarune launcher and chapters 1, 2 and 5, and the TEST project built with runtime 2026.0.0.23). UTMT was never built or run.
- Column "UTMT" and "G3MLib": yes, partial or no. "partial" means the field is read and written but a defect or a deliberate deviation changes the result.
- Both libraries share the same set of chunk names: GEN8 OPTN LANG EXTN SOND AGRP SPRT BGND PATH SCPT GLOB GMEN SHDR FONT TMLN OBJT ROOM UILR DAFL TPAG CODE VARI FUNC STRG TXTR AUDO EMBI TGIN ACRV SEQN TAGS FEDS FEAT PSEM PSYS. No other chunk name is handled by UTMT. An unknown chunk name throws in both (see gap 2).

Class line numbers in the chunk file (UTMT `UndertaleChunks.cs` / G3MLib `GameMakerChunks.cs`): GEN8 200/215, OPTN 222/238, LANG 227/243, EXTN 237/253, SOND 402/430, AGRP 477/512, SPRT 595/630, BGND 762/799, PATH 868/905, SCPT 873/910, GLOB 878/915, GMEN 883/920, SHDR 888/925, FONT 931/970, TMLN 1262/1307, OBJT 1267/1312, ROOM 1348/1388, UILR 1633/1690, DAFL 1638/1695, TPAG 1643/1700, CODE 1688/1746, VARI 1740/1803, FUNC 1822/1889, STRG 1961/2028, TXTR 1987/2054, AUDO 2215/2282, EMBI 2243/2310, TGIN 2277/2344, ACRV 2381/2466, SEQN 2476/2548, TAGS 2537/2596, FEDS 2588/2634, FEAT 2640/2673, PSEM 2683/2761, PSYS 2823/2927.

## 1. Coverage tables, one per chunk

Shared G3MLib hardening that applies to every chunk below and is not repeated: reads are bounds-checked against the FORM and the remaining input, counts are validated before allocation, null resource references are tolerated, and unset optional fields are reset to defaults when a version gate is false. These do not change the on-disk format.

### GEN8 (general info)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| IsDebuggerDisabled, BytecodeVersion, padding u16 | Bytecode 13 to 17 supported; outside that, `UnsupportedBytecodeVersion` | yes | yes | Models/GameMakerGeneralInfo.cs | CODE, VARI, FUNC kept raw when unsupported |
| FileName, Config, LastObj, LastTile, GameID, DirectPlayGuid, Name | none | yes | yes | Models/GameMakerGeneralInfo.cs | |
| Version (four u32: major, minor, release, build) | Written as stored when major < 2; written as 2.0.0.0 when major >= 2 | yes | partial | Models/GameMakerGeneralInfo.cs 296, 443, 534; GameMakerChunks.cs 233 | Read result is discarded in G3MLib, see section 3 and gap 1 |
| DefaultWindowWidth/Height, Info flags, LicenseCRC32, LicenseMD5, Timestamp, DisplayName | none | yes | yes | Models/GameMakerGeneralInfo.cs | |
| ActiveTargets, FunctionClassifications, SteamAppID, RoomOrder | none | yes | yes | Models/GameMakerGeneralInfo.cs | |
| DebuggerPort | `BytecodeVersion >= 14` | yes | yes | Models/GameMakerGeneralInfo.cs | |
| Random UID block (first random, four slots, info number), GMS2FPS, GMS2AllowStatistics, GMS2GameGUID | `Major >= 2` | yes | partial | Models/GameMakerGeneralInfo.cs | Gated on a major version that G3MLib loses for files without marker chunks (gap 1) |
| Version inference from chunk names | UILR 2024.13 (non-LTS), PSEM 2023.2 (non-LTS), FEAT 2022.8, FEDS 2.3.6, SEQN 2.3, TGIN 2.2.1 | yes | yes | Models/GameMakerGeneralInfo.cs 407 | Same order and values; UTMT `UndertaleGeneralInfo.cs` 450 |
| Branch (Pre2022_0, LTS2022_0, Post2022_0) | LTS is set at FORM end when version >= 2023.1 and no non-LTS marker was seen | yes | yes | GameMakerChunks.cs 157 | UTMT `UndertaleChunks.cs` 149 |

### OPTN (options)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| NewFormat flag, Info flags, Scale, WindowColor, ColorDepth, Resolution, Frequency, VertexSync, Priority | none | yes | yes | Models/GameMakerGeneralInfo.cs | Options class lives in the GEN8 model file |
| BackImage, FrontImage, LoadImage, LoadAlpha | none | yes | yes | Models/GameMakerGeneralInfo.cs | |
| ShaderExtensionFlag, ShaderExtensionVersion | new-format only | yes | yes | Models/GameMakerGeneralInfo.cs | G3MLib resets both to 0 for the old format |
| Constants (name, value) | none | yes | yes | Models/GameMakerGeneralInfo.cs | |

### LANG (languages)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Unknown1, LanguageCount, EntryCount, entry IDs | none | yes | yes | Models/GameMakerGeneralInfo.cs | |
| Languages (Name, Region, entries) | none | yes | yes | Models/GameMakerGeneralInfo.cs | G3MLib checks the pointer count against input length |

### EXTN (extensions)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| FolderName, Name, ClassName | none | yes | yes | Models/GameMakerExtension.cs | |
| Version string | `IsVersionAtLeast(2023, 4)` = GM 2023.4 | yes | yes | Models/GameMakerExtension.cs | Detected by pointer arithmetic, UTMT chunks 320, G3MLib chunks 361 |
| Files and Options as separate pointer lists | `IsVersionAtLeast(2022, 6)` = GM 2022.6 | yes | yes | Models/GameMakerExtension.cs | Detection UTMT chunks 244, G3MLib chunks 332 |
| Files, functions, args, kind and option records | none | yes | yes | Models/GameMakerExtension.cs | |
| Product ID blobs (16 bytes per extension) | `Major >= 2`, or GMS1 build 1559 or >= 1773 | yes | partial | Models/GameMakerExtension.cs 473; GameMakerChunks.cs 392 | Build is lost by gap 1, so GMS 1.4 builds >= 1773 read as having no product IDs |
| Null extension slot | n/a | n/a | n/a | GameMakerChunks.cs 392 | G3MLib rejects null slots on write |

### SOND (sounds)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Name, Flags, Type, File, Effects, Volume, Pitch, AudioGroup, AudioFile/id | none | yes | yes | Models/GameMakerSound.cs | UTMT initial `Flags` default used `&`; G3MLib corrected it to `\|` |
| Preload (regular sounds) | `BytecodeVersion >= 14` | yes | yes | Models/GameMakerSound.cs | |
| AudioLength | `IsVersionAtLeast(2024, 6)` = GM 2024.6 | yes | partial | Models/GameMakerSound.cs | G3MLib gates on `IsNonLTSVersionAtLeast`, so it differs only when Branch is LTS (gap 6) |
| 2024.6 detection | pointer spacing of 4*9 bytes between first two sounds | yes | yes | GameMakerChunks.cs 471 | UTMT chunks 436 |

### AGRP (audio groups)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Name | none | yes | yes | Models/GameMakerAudioGroup.cs | |
| Path | `IsVersionAtLeast(2024, 14)` = GM 2024.14 | yes | yes | Models/GameMakerAudioGroup.cs | Detection by non-zero path pointer or pointer spacing, chunks 522 |

### SPRT (sprites)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Name, size, margins, bbox mode, sep masks, origin, textures, collision masks | none | yes | yes | Models/GameMakerSprite.cs | |
| GMS2 playback speed and type, sequence, nine slice | `IsGameMaker2()`; sequence and slice offsets set 2.3.2 | yes | yes | Models/GameMakerSprite.cs | UTMT sprite file lines 617 to 627 |
| Collision mask bbox dimensions | `IsVersionAtLeast(2024, 6)` = GM 2024.6 | yes | partial | Models/GameMakerSprite.cs | G3MLib gates on non-LTS (gap 6) |
| Spine data (json, atlas, textures) | sprite type Spine | yes | yes | Models/GameMakerSprite.cs | |
| SWF data (timeline, shapes, bitmap, gradient fill TPEIndex) | TPEIndex at `IsVersionAtLeast(2022, 1)` | yes | yes | Models/GameMakerSprite.cs | |
| Vector data (version 3 or 4) | vector version 4 implies 2024.14 | yes | yes | Models/GameMakerSprite.cs | UTMT sprite 721; G3MLib 750 |
| 2024.6 chunk detection | collision mask sizes in first sprites | yes | yes | GameMakerChunks.cs 762, 769 | G3MLib skips the set on LTS branch, which cannot be true during a read, so no practical effect |

### BGND (backgrounds and tilesets)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Name, Transparent, Smooth, Preload, Texture | none | yes | yes | Models/GameMakerBackground.cs | |
| Tileset version, tile size, border, columns, items per tile, tile count, frame length, tile ids | `IsGameMaker2()` | yes | yes | Models/GameMakerBackground.cs | |
| Exported sprite reference | `IsVersionAtLeast(2023, 8)` = GM 2023.8 | yes | yes | Models/GameMakerBackground.cs | Reading a non-zero id also sets 2023.8 |
| TileSeparationX/Y | `IsVersionAtLeast(2024, 14, 1)` = GM 2024.14.1 | yes | yes | Models/GameMakerBackground.cs | Detection by chunk length, chunks 854, 868 |

### PATH

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Name, IsSmooth, IsClosed, Precision, points (X, Y, Speed) | none | yes | yes | Models/GameMakerPath.cs | |

### SCPT (scripts)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Name, code id | none | yes | yes | Models/GameMakerScript.cs | |
| IsConstructor flag in the code id (high bit) | GMS2.3 and later use | yes | yes | Models/GameMakerScript.cs | G3MLib resets to false when absent |

### GLOB and GMEN (global init scripts)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Code reference list | none | yes | yes | Models/GameMakerGlobalInit.cs | GMEN is the legacy name handled with the same class |

### SHDR (shaders)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Name, Type, GLSL ES, GLSL, HLSL9 vertex and fragment sources | none | yes | yes | Models/GameMakerShader.cs | |
| Vertex attribute list, version | `BytecodeVersion > 13` | yes | yes | Models/GameMakerShader.cs | |
| HLSL11, PSSL, Cg PSVita, Cg PS3 blobs | `BytecodeVersion > 13` | yes | yes | Models/GameMakerShader.cs | G3MLib defaults the platform blobs when version is 13 |
| Entry end pointer | none | yes | yes | Models/GameMakerShader.cs | G3MLib adds an entry count check, chunks 937 |

### FONT

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Name, DisplayName, EmSize (int or float), Bold, Italic, RangeStart/End, Charset, AntiAliasing, Texture, ScaleX/Y | none | yes | yes | Models/GameMakerFont.cs | |
| AscenderOffset | `BytecodeVersion >= 17` | yes | yes | Models/GameMakerFont.cs | |
| Ascender | `IsVersionAtLeast(2022, 2)` = GM 2022.2 | yes | yes | Models/GameMakerFont.cs | Detection chunks 1008/1047 and sequences 668/690 |
| SDFSpread | `IsNonLTSVersionAtLeast(2023, 2)` = GM 2023.2 non-LTS | yes | yes | Models/GameMakerFont.cs | |
| LineHeight | `IsVersionAtLeast(2023, 6)` = GM 2023.6 | yes | yes | Models/GameMakerFont.cs | Detection chunks 1152/1190 |
| Glyph list, glyph kerning list | none | yes | yes | Models/GameMakerFont.cs | |
| Glyph UnknownAlwaysZero | `IsVersionAtLeast(2024, 11)` = GM 2024.11 | yes | partial | Models/GameMakerFont.cs | G3MLib gates on non-LTS (gap 6) |
| End-of-font alignment | `IsVersionAtLeast(2024, 14)` = GM 2024.14 | yes | yes | Models/GameMakerFont.cs | |

### TMLN (timelines)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Name, moments (step, event list) | none | yes | yes | Models/GameMakerTimeline.cs | |

### OBJT (game objects)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Name, sprite, visible, solid, depth, persistent, parent, texture mask, physics fields, vertices | none | yes | yes | Models/GameMakerGameObject.cs | |
| Managed (rollback) | `IsVersionAtLeast(2022, 5)` and not `IsVersionAtLeast(2026, 1)` = GM 2022.5 to 2026.0 | yes | yes | Models/GameMakerGameObject.cs 224 | Absence of the field is the only 2026.1 marker, see section 2 |
| Event lists (15 types), event actions | none | yes | yes | Models/GameMakerGameObject.cs | |
| 2022.5 detection (Managed present) | probe at object offset 64 | yes | yes | GameMakerChunks.cs 1316 to 1363 | UTMT chunks 1274 |

### FEDS (filter effects)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Chunk version 1, name, filter name, parameter values | chunk exists from 2.3.6 | yes | yes | Models/GameMakerFilterEffect.cs | Versioned header via `GameMakerChunkVersion` helper |

### ACRV (animation curves)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Name, graph type, channels, points | none | yes | yes | Models/GameMakerAnimationCurve.cs | |
| Bezier handle fields in points | `IsVersionAtLeast(2, 3, 1)` = GMS 2.3.1 | yes | yes | Models/GameMakerAnimationCurve.cs | Detected from chunk, chunks 2496 to 2501 |

### SEQN (sequences)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Name, playback, speed, length, origin, volume, broadcast messages, tracks, keyframes | none | yes | yes | Models/GameMakerSequence.cs | |
| Width, Height | `IsVersionAtLeast(2024, 13)` = GM 2024.13 | yes | yes | Models/GameMakerSequence.cs | |
| Text track WrapMode, Origin | `IsVersionAtLeast(2024, 14)` = GM 2024.14 | yes | yes | Models/GameMakerSequence.cs | |
| Moment and event function ids | `!IsVersionAtLeast(2022, 2)` branch detection | yes | yes | Models/GameMakerSequence.cs | |
| GMAssetTrack | n/a | no | no | Models/GameMakerSequence.cs 574, 673 | Both throw "not implemented" (gap 3) |

### TAGS

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Tag list, asset tag map | chunk exists from 2.3 | yes | yes | Models/GameMakerTags.cs | No version marker from TAGS |

### ROOM

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Name, caption, size, speed, flags, colour, backgrounds, views, game objects, tiles | none | yes | yes | Models/GameMakerRoom.cs | |
| Physics world | `IsGameMaker2()` or bytecode 16 variants | yes | yes | Models/GameMakerRoom.cs | |
| Image speed and index per instance | `IsVersionAtLeast(2, 2, 2, 302)` = GMS 2.2.2.302 | yes | yes | Models/GameMakerRoom.cs | Detected by 48-byte instance spacing, chunks 1570 |
| Layers, layer data, sequences list | `IsVersionAtLeast(2, 3)` = GMS 2.3 | yes | yes | Models/GameMakerRoom.cs | |
| Effect data per layer | `IsVersionAtLeast(2022, 1)` = GM 2022.1 | yes | yes | Models/GameMakerRoom.cs | Detected from layer sizes, chunks 1484 to 1515 |
| Tile layer compression (RLE) | `IsVersionAtLeast(2024, 2)` = GM 2024.2 | yes | yes | Models/GameMakerRoom.cs | |
| Tile layer alignment | `IsVersionAtLeast(2024, 4)` = GM 2024.4 | yes | yes | Models/GameMakerRoom.cs | |
| Nine slices, particle systems, text items, instance asset lists | `IsVersionAtLeast(2024, 6)` = GM 2024.6 (plus `IsNonLTSVersionAtLeast(2023, 2)` for particle and text items) | yes | partial | Models/GameMakerRoom.cs | G3MLib gates the 2024.6 items on non-LTS (gap 6) |
| InstanceCreationOrderIDs | `IsVersionAtLeast(2024, 13)` = GM 2024.13 | yes | yes | Models/GameMakerRoom.cs | |
| Text item WrapMode, Origin | `IsVersionAtLeast(2024, 14)` = GM 2024.14 | yes | yes | Models/GameMakerRoom.cs | |

### UILR (UI layers)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Root nodes, node types (flex panel, sprite, text, and others), child lists | chunk exists from 2024.13 | yes | yes | Models/GameMakerUINode.cs | Chunk name alone sets 2024.13 non-LTS |

### DAFL (data files)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Empty chunk, no content | n/a | yes | yes | GameMakerChunks.cs 1695 | Included data files are not stored in the chunk |

### EMBI (embedded images)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Chunk version 1, texture page item references | `IsGameMaker2()` | yes | yes | Models/GameMakerEmbeddedImage.cs | |

### TPAG (texture page items)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Source rect, target rect, bounding size, texture index | none | yes | yes | Models/GameMakerTexturePageItem.cs | |
| 4-byte alignment of the chunk | detected by layout (`IsTPAG4ByteAligned`) | yes | yes | GameMakerChunks.cs 1708 to 1735 | |

### TGIN (texture group info)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Name, texture pages, sprites, fonts, tilesets | chunk exists from 2.2.1 | yes | yes | Models/GameMakerTextureGroupInfo.cs | |
| Directory, Extension, LoadType | `IsVersionAtLeast(2022, 9)` = GM 2022.9 | yes | yes | Models/GameMakerTextureGroupInfo.cs | Detection by pointer-in-object test, chunks 2376 |
| Spine sprites list | not `IsNonLTSVersionAtLeast(2023, 1)` branch = GM 2023.1 non-LTS | yes | yes | Models/GameMakerTextureGroupInfo.cs | Sets 2023.1 and non-LTS, chunks 2398 |

### CODE

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Code entry header (name, length, locals, args, offset) | none | yes | yes | Models/GameMakerCode.cs | |
| Bytecode 14 and lower layout | `Bytecode14OrLower` | yes | yes | Models/GameMakerCode.cs | |
| Instruction set, all opcodes | none | yes | yes | Models/GameMakerCode.cs | |
| Int32 argument on push of asset ref (2023.8 asset-ref form) | `IsVersionAtLeast(2023, 8)` = GM 2023.8 | yes | yes | Models/GameMakerCode.cs 795, 908 | UTMT 794, 907 |
| New asset type numbering | `IsVersionAtLeast(2024, 4)` = GM 2024.4 | yes | partial | Models/GameMakerCode.cs 1274 to 1360 | G3MLib omits `AudioGroup` in the id mapping (gap 4) |
| chknullish instruction (-10) | `IsVersionAtLeast(2, 3, 7)` = GMS 2.3.7 | yes | yes | Models/GameMakerCode.cs 815 | |
| Dup with 16-bit extra data | `IsVersionAtLeast(2024, 14, 4)` = GM 2024.14.4 | yes | yes | Models/GameMakerCode.cs 716 | |
| Unsupported bytecode (13 to 17 outside) | `UnsupportedBytecodeVersion` | yes | yes | Models/GameMakerCode.cs | Kept raw |

### VARI

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Header counts (varCount1, varCount2, maxLocalVarCount) | `BytecodeVersion >= 15` | yes | yes | GameMakerChunks.cs 1803 | |
| Variable records and reference chains | none | yes | yes | Models/GameMakerVariable.cs | |

### FUNC

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Function records and reference chains | none | yes | yes | Models/GameMakerFunction.cs | |
| CodeLocals list | `IsVersionAtLeast(2, 3)` = GMS 2.3 | yes | yes | Models/GameMakerFunction.cs | |
| Layout without Functions/CodeLocals split | `!IsVersionAtLeast(2024, 8)` = before GM 2024.8 | yes | yes | GameMakerChunks.cs 1889 to 2020 | Detection chunks 1919, 1945 |
| 2023.11 script-name form | function names with `@` | yes | yes | GameMakerIO.cs 336 to 345 | Post-read name scan |

### STRG (strings)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| String table with 4-byte aligned data | none | yes | yes | Models/GameMakerString.cs | `GameMakerAlignUpdatedListChunk` |

### TXTR (embedded textures)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Scaled, texture data offset | `IsGameMaker2()`; GMS2 0.6 adds `GeneratedMips` | yes | yes | Models/GameMakerEmbeddedTexture.cs | Detection chunks 2192 (UTMT 2127) |
| Texture block size | `IsVersionAtLeast(2022, 3)` = GM 2022.3 | yes | yes | Models/GameMakerEmbeddedTexture.cs | Detection chunks 2090 |
| External texture flag, directory index | `IsVersionAtLeast(2022, 9)` = GM 2022.9 | yes | yes | Models/GameMakerEmbeddedTexture.cs | |
| QOI and BZ2 QOI images | `IsVersionAtLeast(2022, 5)` = GM 2022.5 | yes | yes | Models/GameMakerEmbeddedTexture.cs | Detection chunks 2124 |
| PNG, QOI, BZ2 QOI decoding and encoding | none | yes | yes | Util/GMImage.cs | |
| Save external texture group file | n/a | no | yes | Models/GameMakerEmbeddedTexture.cs | G3MLib addition (`SaveExternalTexture`) |

### AUDO (embedded audio)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Audio blob list | none | yes | yes | Models/GameMakerEmbeddedAudio.cs | G3MLib checks blob length against input |

### FEAT (feature flags)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Chunk version 1, feature name list | chunk exists from 2022.8 | yes | yes | Models/GameMakerFeatureFlags.cs | Chunk name alone sets 2022.8 |

### PSEM (particle system emitters)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Chunk version 1, emitter list | chunk exists from 2023.2, non-LTS | yes | yes | Models/GameMakerParticleSystem.cs | |
| Emitter layout steps | 2023.4 (0xBC spacing, or whole-chunk 0xC8), 2023.6 (0xC0, 0xD8), 2023.8 (0xEC, 0xF8); 0xB0 is 2023.2 | yes | yes | GameMakerChunks.cs 2820 to 2900 | Same thresholds |
| Null emitter slots | implies 2024.11 | no | yes | GameMakerChunks.cs 2842 | G3MLib extension; UTMT PSEM has no null-slot handling |
| Per-chunk emitter format override (`WithPsemFormat`) | n/a | n/a | partial | GameMakerChunks.cs 2775 to 2803 | Defined but never called (gap 12) |

### PSYS (particle systems)

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Chunk version 1, name, origin, draw order, emitters | chunk exists from 2023.2 | yes | yes | Models/GameMakerParticleSystem.cs | |
| GlobalSpaceParticles | `IsVersionAtLeast(2023, 8)` = GM 2023.8 | yes | yes | Models/GameMakerParticleSystem.cs | |

### Other structures that are not chunks

| Field or structure | Version condition | UTMT | G3MLib | G3MLib file | Notes |
| --- | --- | --- | --- | --- | --- |
| Pointer lists with null slots | implies 2024.11 | yes | yes | GameMakerLists.cs 475, 566, 605; GameMakerIO.cs 128 | G3MLib has three list sites to UTMT two |
| Debug data (YYD): FORM, SCPT, DBGI, INST, LOCL, STRG | n/a | yes | yes | GameMakerDebugChunks.cs, ModelsDebug/ | G3MLib routes unknown debug chunks to an unsupported-chunk class |
| Per-chunk 16-byte padding and alignment exceptions | `Major >= 2` or GMS1 `Build >= 9999` | yes | partial | GameMakerChunkTypes.cs 44, 106 | Build is lost by gap 1, so GMS 1.4.9999 padding is not applied |
| Project export (assets to files) | n/a | no | yes | Project/ | G3MLib only |

## 2. Version detection

### How UTMT infers each step

UTMT reads GEN8 first (`UndertaleChunks.cs` 200 to 220). It keeps the stored 4 numbers if the major is below 2, then lets chunk names override (`TestForCommonGMSVersions`, `UndertaleGeneralInfo.cs` 450). Every other step is raised during the reading pass by `SetGMS2Version`, which only raises the version when the observed layout needs it. The main checks, all in `UndertaleChunks.cs` unless noted:

| GameMaker version | Trigger | UTMT line | G3MLib line |
| --- | --- | --- | --- |
| 2.0.6 | TXTR entry spacing of 8 bytes ("Scaled" plus "_textureData") | 2127 | 2192 |
| 2.2.1 | chunk TGIN present | GeneralInfo 459 | GeneralInfo 413 |
| 2.2.2.302 | first two room instances 48 bytes apart | 1520 | 1570 |
| 2.3 | chunk SEQN present | GeneralInfo 457 | GeneralInfo 411 |
| 2.3.1 | animation curve point layout (BezierX0 check) | 2411, 2416; AnimationCurve 200, 205 | 2496, 2501; 200, 205 |
| 2.3.2 | sprite nine slice offset | Sprite 627, 816 | Sprite 653, 861 |
| 2.3.6 | chunk FEDS present | GeneralInfo 455 | GeneralInfo 409 |
| 2.3.7 | chknullish instruction (-10) | Code 814 | Code 815 |
| 2022.1 | room layer data sizes with effect data | 1442 to 1467 | 1484 to 1515 |
| 2022.2 | FONT glyph layout; SEQN | 1008; Sequence 668 | 1047; Sequence 690 |
| 2022.3 | TXTR block layout | 2023 | 2090 |
| 2022.5 | OBJT has Managed field; TXTR QOI | 1312; 2057 | 1358; 2124 |
| 2022.6 | EXTN pointer layout | 316 | 332 |
| 2022.8 | chunk FEAT present | GeneralInfo 453 | GeneralInfo 409 |
| 2022.9, 2023.1 | TGIN pointer-in-object test and fourth-list count | 2309, 2331 | 2376, 2398 |
| 2023.2 | chunk PSEM present (non-LTS) | GeneralInfo 451 | GeneralInfo 407 |
| 2023.4 | EXTN Version string; PSEM 0xBC spacing | 345; 2706, 2733, 2762 | 361; PSEM through `SetPsemFormat` |
| 2023.6 | FONT glyph pointer check; PSEM 0xC0, 0xD8 | 1152; 2727, 2757 | 1190 |
| 2023.8 | push with Int32 argument; BGND exported sprite; PSEM 0xEC, 0xF8 | Code 794, 907; Background 245; 2721, 2752 | Code 795, 908; Background 284 |
| 2023.11 | function name with `@` | IO 345 | IO 345 |
| 2024.2, 2024.4 | ROOM tile layer size or alignment; CODE asset type id | 1614, 1624; Code 801 | 1671, 1681; Code 802 |
| 2024.6 | SOND pointer spacing 4*9; SPRT mask sizes; ROOM data before legacy tiles | 436, 451, 726, 732; Room 2163, 2200 | 471, 486, 762, 769; Room 2276, 2324 |
| 2024.8 | FUNC layout and CODE count | 1852, 1878 | 1919, 1945 |
| 2024.11 | null pointer in any list or resource ref; FONT glyph zero field; PSEM null slot (G3MLib only) | 1147; Lists 517, 610; IO 134 | 1186; Lists 475, 566, 605; IO 128, 361; 2842 |
| 2024.13 | chunk UILR present (non-LTS) | GeneralInfo 451 | GeneralInfo 405 |
| 2024.14 | AGRP path pointer; FONT; SPRT vector version 4 | 573, 1205; Sprite 721 | 608, 1250; Sprite 750 |
| 2024.14.1 | BGND chunk length vs tile count | 817, 831 | 854, 868 |
| 2024.14.4 | Dup with 16-bit extra data | Code 714 | Code 716 |
| 2026.1 | OBJT without the Managed field (and version already >= 2024.13) | 1317 | 1362 |

(The GeneralInfo line numbers above for chunk-name rules are UTMT `UndertaleGeneralInfo.cs` 450 to 466 and G3MLib `GameMakerGeneralInfo.cs` 407 to 424; the exact line per rule is one of those lines, in the order UILR, PSEM, FEAT, FEDS, SEQN, TGIN.)

### How G3MLib differs

The set of triggers is the same. A comparison of every `SetGMS2Version` call by file and argument shows the same values; G3MLib only adds the PSEM wrapper, a second FONT site, the debug-data reader and a third list site. Differences that matter:

1. G3MLib starts from the wrong stored version (gap 1). The call `Object.Version.Unserialize(reader)` runs on a struct returned by a property (`GameMakerChunks.cs` 233), so it mutates a temporary copy and the real value stays at the default 1.0.0.1337. UTMT declares `Version` as a public field (`UndertaleGeneralInfo.cs` 340), so the same call mutates the stored value. In G3MLib, `GameMakerGeneralInfo.cs` 534 repeats the pattern. Result: every file starts detection from 1.0.0.1337 whatever GEN8 stores. A probe confirmed this: reading only GEN8 of the TEST project (stored 2.0.0.0, bytes `02 00 00 00` at file offset 60) reports `1.0.0.1337`.
2. Branch gating of 2024.6 and 2024.11 features. G3MLib replaced `IsVersionAtLeast` with `IsNonLTSVersionAtLeast` for the SOND AudioLength, SPRT bbox masks, ROOM 2024.6 items and FONT glyph zero field, and added `IsLTSBranch()` guards on two SPRT detections. During a read the branch is never LTS, because LTS is only set at the end of the FORM (`GameMakerChunks.cs` 157), so reads match UTMT. Writes differ for LTS-branch data (gap 6).
3. PSEM. UTMT sets 2023.4, 2023.6 or 2023.8 and relies on the global version. G3MLib records the chunk's own layout (`psemFormatMinor`), counts null slots separately, and treats any null slot as 2024.11 plus layout 8 (`GameMakerChunks.cs` 2842). The helper that was meant to apply the chunk layout during emitter reads is never called (gap 12).
4. 2026.1. Both set 2026.1 from the OBJT probe only (`GameMakerChunks.cs` 1360 to 1363). Same rule.
5. Chunk walk. G3MLib bounds the chunk scan to the FORM length (`GameMakerChunks.cs` 122, 170). UTMT scans to the end of the stream (`UndertaleChunks.cs` 121, 162). See gap 5.

### Why "GM 2024.14" for a build with runtime 2026.0.0.23

The text comes from `GetVersionDisplay` (`G3MLib/G3MLib/Modding/Internal/GeneralInfoUtil.cs` 42 to 80), which prints `GM {major}.{minor}` and appends release and build only when non-zero. The numbers come from detection on the TEST project (one sprite, one object, one room):

1. Chunk list: UILR and PSEM are present, so `TestForCommonGMSVersions` sets 2024.13 and non-LTS (`GameMakerGeneralInfo.cs` 405 to 410).
2. AGRP has a non-zero path pointer (stored group entry has the extra path field), so `CheckForGM2024_14` sets 2024.14 (`GameMakerChunks.cs` 608).
3. The only later marker is the 2026.1 rule in OBJT (`GameMakerChunks.cs` 1316 to 1363), which needs the Managed field to be absent. In this file it is present: the dword at object offset 64 is `0x3DCCCCCD` (a float 0.1 physics field) and not a small vertex count, so the probe concludes "Managed present" and sets only 2022.5, which is already lower than the current version. No 2026.1 is set.
4. There is no detection step between 2024.14 and 2026.1 in UTMT or G3MLib, and none for a runtime numbered 2026.0. Both libraries therefore stop at 2024.14 for this file. Writing the file back reproduces it byte for byte (0 differing bytes of 4,428), so the missing label did not cost data for this minimal project. Whether richer 2026.0 projects carry fields that neither library detects is untested (phase L1 question 1 stays open).

### Why "GM 2022.0 (raw: 2023.6.0.0)" for Deltarune

Facts from the files. The Deltarune launcher and chapters 1, 2 and 5 contain neither PSEM nor UILR. They contain FEAT (so 2022.8) and FONT passes the glyph-pointer test (`GameMakerChunks.cs` 1190 sets 2023.6).

1. Detection result is version 2023.6.0.0 (probe output: `ver=2023.6.0.0 branch=LTS2022_0` for the launcher, chapter 1, chapter 2, chapter 5).
2. At the end of the FORM, version >= 2023.1 and no non-LTS marker was found (branch is still `Pre2022_0`), so `SetLTS(true)` runs (`GameMakerChunks.cs` 157 to 161). Branch becomes `LTS2022_0`. UTMT does the same (`UndertaleChunks.cs` 149 to 152).
3. `GetVersionDisplay` replaces the interpreted version with a hard-coded `"2022.0"` whenever the branch is LTS (`GeneralInfoUtil.cs` 46 to 49). Raw is `2023.6.0.0`. Since they differ and the branch is LTS, it appends `(raw: ...)`. The prefix is `GM` because the major is 2023. This copies UTMT's `ToString` (`UndertaleGeneralInfo.cs` 696).
4. So "2022.0" is a label for the LTS branch and not a detected release number. "2023.6" is the highest layout step detected on that branch. The same label would print for a 2026 LTS file (gap 10). `DataIntegrityService.RepairGeneralInfoBranch` (`Modding/Integrity/DataIntegrityService.cs` 54 to 62) is a G3MLib-only repair that restores LTS for exactly 2023.6.0.0 bytecode 17 data.

## 3. GEN8 write-back: Undertale build 1539 becomes 1337

Observed. Undertale (Steam, 62,921,978 bytes, bytecode 16) stores version 1.0.0.1539 in GEN8. Bytes at file offsets 72 and 73 (GEN8 data offset 56, the build field, chunk data starts at file offset 16) are `03 06`, which is 0x0603 = 1539. After a G3MLib read and write the file has the same length and exactly two bytes differ: `03 06` becomes `39 05`, which is 0x0539 = 1337.

Correction to earlier notes. `docs/research/gamemaker-format.md` (question 4) says `47 05` (1351). The measured value is `39 05` (1337), on both the current Release build and the older G3MLib copy in the G3MTool CLI output folder. 1337 is also the value G3MLib reports for Undertale ("GMS 1.0.0.1337"). The note should be corrected to 1337.

Cause, with code:

1. Version struct. `RuntimeVersion` has a default constructor that sets `Major = 1` and `Build = 1337` (`G3MLib/G3MLib/DataFile/Models/GameMakerGeneralInfo.cs` 270 to 274). `GameMakerGeneralInfo.Version` is an auto-property `{ get; set; } = new()` (line 296).
2. GEN8 prepass: `Object.Version.Unserialize(reader)` (`GameMakerChunks.cs` 233). `Unserialize` is an instance method of a struct. Calling it through a property getter runs it on a copy, so the 16 bytes read are thrown away. The compiler accepts this silently.
3. `Object.Version = TestForCommonGMSVersions(reader, Object.Version)` (line 234) then runs on the untouched default 1.0.0.1337. No chunk name rule matches Undertale, so the value stays.
4. Full GEN8 read repeats the pattern at `GameMakerGeneralInfo.cs` 534 (`Version.Unserialize(reader)`) and then assigns `Version = reader.gameMakerData.GeneralInfo.Version` (line 540), the prepass value.
5. Writing: `RuntimeVersion versionToWrite = Major < 2 ? Version : new(2); versionToWrite.Serialize(writer);` (line 443). Undertale has major 1, so the in-memory 1.0.0.1337 is written. For major 2 and above G3MLib always writes 2.0.0.0 (also what UTMT does, by design: "no longer updated").

How UTMT handles the same field. `UndertaleGeneralInfo.cs` 340 declares `public RuntimeVersion Version;` as a field. The same call `Object.Version.Unserialize(reader)` (`UndertaleChunks.cs` 217) mutates the stored value in place, so major, minor, release and build are kept (1.0.0.1539). `TestForCommonGMSVersions` (line 450) only overrides on chunk names, and the write at `UndertaleGeneralInfo.cs` 488 writes the stored 1.0.0.1539 back. Round trip is byte identical for Undertale.

Other effects of losing the build number (all in G3MLib 1.0.1):

- `GlobalDecompileContext.UsingStringRealOptimizations` tests `Build is 1539 or >= 1763` (`Decompiler/GlobalDecompileContext.cs` 33). Undertale now takes the wrong branch.
- `ProductDataEligible` tests `Build >= 1773 || Build == 1559` (`Models/GameMakerExtension.cs` 473 to 486), so GMS 1.4 files from builds >= 1773 with extensions would be parsed without their product ID blobs.
- Chunk padding tests `Major >= 2 || (Major == 1 && Build >= 9999)` (`GameMakerChunkTypes.cs` 44, 106), so GMS 1.4.9999 files lose the 16-byte chunk alignment rule.
- `BuiltinList` picks tables from `Build` (`Compiler/BuiltinList.cs` 242).
- A GMS2 file with no marker chunk (no TGIN, so before 2.2.1) starts from major 1 instead of 2, so `IsGameMaker2()` is false and GMS2-only fields (such as the GEN8 random UID block) are skipped. A synthetic copy of the TEST project with TGIN, SEQN, FEDS, FEAT, PSEM and UILR renamed fails to read ("Simple list data exceeds the available input"). That test is not conclusive on its own because the renamed chunks also change the layout the reader expects; a real GMS 2.0 to 2.2.0 sample is needed.

## 4. Gap list for G3MLib 1.1.0

Severity: breaks files, loses data, cosmetic. Fourteen gaps: 5 break files, 3 lose data, 6 cosmetic (gap 14 added from fixtures). A fixture note: no existing G3MLib test covers GEN8 version fields (a search of `G3MLib.Tests` finds only `BuiltinListTests` and `DataIntegrityServiceTests` touching `GeneralInfo`).

| # | Severity | Gap | UTMT reference | G3MLib location | Suggested test |
| --- | --- | --- | --- | --- | --- |
| 1 | breaks files | GEN8 stored version is discarded on read (struct copy). Undertale build 1539 is written as 1337; GMS 1.4.1773+ extensions, GMS 1.4.9999 padding and GMS 2.0 to 2.2.0 detection depend on the lost value | `UndertaleModLib/UndertaleChunks.cs` 217; `UndertaleModLib/Models/UndertaleGeneralInfo.cs` 340, 583 | `Models/GameMakerGeneralInfo.cs` 296, 534; `GameMakerChunks.cs` 233 | Read the Undertale corpus file, assert `Build == 1539`, write, assert byte-identical output. Read GEN8 only on a file that stores 2.0.0.0 and no marker chunks, assert `Major == 2`. Add a fixture for a GMS 1.4.9999 and a GMS 2.0 file |
| 2 | breaks files | Unknown chunk name throws `IOException`; no raw pass-through, so any chunk added by a future runtime blocks the whole file. Shared with UTMT | `UndertaleModLib/UndertaleChunkTypes.cs` (chunk factory, "Unknown chunk") | `GameMakerChunkTypes.cs` (`Unserialize`, "Unknown chunk"); `GameMakerUnsupportedChunk` exists but is wired only for debug data | Inject a chunk named `ZZZZ` before STRG in the TEST build, assert read does not throw and write keeps its bytes |
| 3 | breaks files | SEQN `GMAssetTrack` throws `NotSupportedException`. Sequences with asset tracks cannot be read or written. Shared with UTMT | `UndertaleModLib/Models/UndertaleSequence.cs` ("GMAssetTrack not implemented") | `Models/GameMakerSequence.cs` 574, 673 | Build a project with a sequence containing an asset track (L1 feature project), read and write, compare bytes |
| 4 | breaks files | `AssetType.AudioGroup` has no id in the asset-type adapters, so instructions that reference an audio group asset cannot be encoded or decoded by name | `UndertaleModLib/Models/UndertaleCode.cs` 1326, 1347 | `Models/GameMakerCode.cs` `AdaptAssetTypeId` (about 1321 to 1360) | Assemble `audio_group_name(audiogroup_default)` style code against a 2024.4+ and a 2.3 data object, round trip |
| 5 | loses data | Bytes after the FORM chunk end are dropped silently on read and not written back. UTMT keeps scanning and either parses or throws | `UndertaleModLib/UndertaleChunks.cs` 121, 162 | `GameMakerChunks.cs` 122 to 143, 170 to 198 | Append 32 bytes to the TEST build: probe shows in 4,460, out 4,428. Decide policy: keep the tail or raise a warning, then assert it |
| 6 | loses data | Branch-dependent gates on write: 2024.6 (SOND AudioLength, SPRT bbox masks, ROOM items) and 2024.11 (FONT glyph zero field) use `IsNonLTSVersionAtLeast`. Data read as >= 2024.6 but marked LTS at FORM end is written in the older layout | `UndertaleModLib/Models/UndertaleSound.cs` 186, 222; `UndertaleSprite.cs` 921, 985; `UndertaleRoom.cs` 2129 to 2223; `UndertaleFont.cs` 181 to 205 | Same files in `Models/`, plus `GameMakerChunks.cs` 157 to 161 | Hand-build data with version 2024.14, branch LTS2022_0, a sound, a sprite with masks and a font; write; compare with the same data on Post2022_0. Then build a real LTS 2026 project (L1) and round trip it |
| 7 | loses data | GMS2+ files are written with GEN8 version 2.0.0.0 whatever they stored. If an early GMS2 runtime stored 2.x numbers, they are lost. Shared with UTMT | `UndertaleModLib/Models/UndertaleGeneralInfo.cs` 488 | `Models/GameMakerGeneralInfo.cs` 443 | Needs a GMS 2.0 to 2.2 sample; assert GEN8 bytes equal after write (L1 builds with old runtimes) |
| 8 | cosmetic | No format marker between 2024.14 and 2026.1; runtime 2026.0.0.23 builds label as 2024.14. VerifyMajorGMS2Version also rejects major 2025 | `UndertaleModLib/UndertaleChunks.cs` 1312 to 1317; `UndertaleModLib/UndertaleData.cs` (`VerifyMajorGMS2Version`) | `GameMakerChunks.cs` 1316 to 1363; `GameMakerData.cs` 506 to 509 | Build the same feature set with 2024.14, 2025.x and 2026.0 runtimes, diff chunk layouts, record any new field before adding a detector |
| 9 | cosmetic | 2026.1 is set when a >= 2024.13 file has zero objects (empty OBJT reads as "Managed absent"). `CreateNew2026LTS` reports branch LTS, then reads back as `2026.1.0.0 Post2022_0` | `UndertaleModLib/UndertaleChunks.cs` 1274 to 1317 | `GameMakerChunks.cs` 1316 to 1363; `GameMakerData.cs` 731 to 820 | Probe output confirms the branch change; assert the intended branch for a fresh 2026 LTS data object after write and read |
| 10 | cosmetic | LTS label is the fixed text "2022.0" for any LTS branch (2026 LTS would print "GM 2022.0 (raw: 2026.1.0.0)"). Deltarune's "2022.0" is a branch name, not a release number | `UndertaleModLib/Models/UndertaleGeneralInfo.cs` 696 | `Modding/Internal/GeneralInfoUtil.cs` 42 to 80 | Unit test `GetVersionDisplay` for LTS with major 2022, 2023 and 2026 and decide the wording |
| 11 | cosmetic | `GetVersionDisplay` hides the detection basis; no way to see which chunk set the version | not in UTMT lib | `Modding/Internal/GeneralInfoUtil.cs` | Expose the winning rule in verbose info, then test with TEST build (UILR then AGRP path) |
| 12 | cosmetic | PSEM `WithPsemFormat` and `psemFormatMinor` are written but the wrapper is never called, so emitters read with the global version | `UndertaleModLib/UndertaleChunks.cs` 2688 to 2765 | `GameMakerChunks.cs` 2775 to 2803 | Read a file with null PSEM slots (2024.11) and one with mixed emitter spacing, assert layout |
| 13 | cosmetic | Existing research note records the Undertale rewrite as 1351 (`47 05`); measured value is 1337 (`39 05`) | n/a | `G3MStudio/docs/research/gamemaker-format.md` question 4 | Correct the note after gap 1 is fixed and re-run the corpus round trip |
| 14 | breaks files | A sequence audio track that references a sound the build left out (sound asset without an audio file) makes G3MLib refuse the whole file: "Invalid value for resource ID of type GameMakerChunkSOND: 0 (there are only 0)". Igor writes such files and the runtime loads them | n/a (found by fixture seq_audio_track with an empty Sound1) | resource reference resolution | Build the fixture with an empty Sound1 and expect read and byte-identical write, keeping the dangling id |

Status 2026-10-25: gaps 1, 2, 4, 5, 6, 9, 10, 11, 12 and 14 are fixed on the local G3MLib branch `l2/format-gaps` (788 tests pass). Gap 3 closed as not applicable: GameMaker 2026's asset compiler never writes GMAssetTrack (decompiled for this one question, see gamemaker-format.md question 6); G3MLib now explains this in its error (branch `l2/asset-track`). Open: 7, 8, plus the follow-ups listed in docs/status.md.

Order of work suggested for 1.1.0: gap 1 first (one-line fix: read into a local, then assign; plus the tests above), then gaps 5, 6 and 2, then the L1 projects that answer gaps 3, 4, 7 and 8.
