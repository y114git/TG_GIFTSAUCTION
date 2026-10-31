# GameMaker fixture results

Generated with `tools/gm-fixtures/gmfixtures.py` on 2026-10-25. G3MLib 1.0.1 through G3MTool CLI 1.3.0, VM output. Base: a project with one asset of every type. Two runs: GameMaker LTS 2026 (runtime 2026.0.0.23, 52 variants) and GameMaker LTS 2022 (runtime 2022.0.3.99, 39 variants).

Columns: "Size changed" lists chunks whose size differs from the base build. "Content only" lists chunks with the same size but different bytes; once STRG grows, absolute pointers shift in later chunks, so that column is noisy for variants that add strings. GEN8 is left out because every build stores a new timestamp and random IDs.

## LTS 2026 (runtime 2026.0.0.23)

Igor exit 1 comes from the empty Sound1 asset in the base project (no audio file); the data file is still written. Variants that add a WAV file exit 0. The variants from `seq_sprite_track` on are the second round: sequences, particles, fonts, extension, UI layer, filters, options and audio group.

| Variant | Igor exit | Size | G3MLib read+write | Byte-identical | Size changed | Content only |
| --- | ---: | ---: | ---: | --- | --- | --- |
| base | 1 | 29,772 | 0 | yes | none | none |
| room_tile_layer | 1 | 29,900 | 0 | yes | ROOM, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, SEQN, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR |
| room_path_layer | 1 | 29,772 | 0 | yes | ROOM, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, SEQN, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT |
| room_asset_layer | 1 | 29,900 | 0 | yes | ROOM | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, SEQN, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT, STRG, TXTR |
| sprite_mask_precise | 1 | 29,772 | 0 | yes | none | SPRT |
| sprite_mask_ellipse | 1 | 29,772 | 0 | yes | none | SPRT |
| sprite_mask_manual | 1 | 29,772 | 0 | yes | none | SPRT |
| sprite_nineslice | 1 | 29,772 | 0 | yes | none | SPRT |
| sprite_tiled_premultiplied | 1 | 29,772 | 0 | yes | none | none |
| sprite_separate_page | 1 | 29,916 | 0 | yes | TXTR | TPAG, TGIN |
| tileset_animation | 1 | 29,900 | 0 | yes | BGND, STRG | OPTN, EXTN, AGRP, SPRT, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR |
| events_all | 1 | 33,100 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| room_instance_view | 1 | 29,772 | 0 | yes | ROOM, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, SEQN, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT |
| texture_png | 1 | 62,636 | 0 | yes | TXTR | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TGIN, CODE, VARI, FUNC, FEAT, STRG |
| texture_qoi | 1 | 34,252 | 0 | yes | TXTR | none |
| texture_bz2 | 1 | 29,772 | 0 | yes | none | none |
| event_destroy | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_alarm0 | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_step | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_step_begin | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_step_end | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_keyboard_left | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_mouse_left_down | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_other_room_start | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_other_user0 | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_async_http | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_draw | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_draw_gui | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_keypress_space | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_keyrelease_space | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_cleanup | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| event_gesture_tap | 1 | 29,900 | 0 | yes | OBJT, CODE, VARI, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, FUNC, FEAT, TXTR |
| sound_c0 | 0 | 74,052 | 0 | yes | SOND, STRG, AUDO | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR |
| sound_c1 | 0 | 34,439 | 0 | yes | SOND, STRG, AUDO | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR |
| sound_c2 | 0 | 34,439 | 0 | yes | SOND, STRG, AUDO | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR |
| sound_c3 | 0 | 29,900 | 0 | yes | SOND, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR |
| seq_sprite_track | 1 | 29,900 | 0 | yes | SEQN, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR |
| seq_instance_track | 1 | 29,900 | 0 | yes | SEQN | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT, STRG, TXTR |
| seq_audio_track | 0 | 74,180 | 0 | yes | SOND, SEQN, STRG, AUDO | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR |
| seq_asset_track | 1 | | | | no data file written | |
| seq_moments_messages | 1 | 30,028 | 0 | yes | SEQN, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR |
| particle_two_emitters | 1 | 30,028 | 0 | yes | PSEM, PSYS | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, SEQN, ROOM, EMBI, TPAG, TGIN, CODE, VARI, FUNC, FEAT, STRG, TXTR |
| font_sdf | 1 | 69,708 | 0 | yes | SHDR, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, FONT, TMLN, OBJT, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR |
| font_extra_range | 1 | 29,772 | 0 | yes | none | none |
| font_bold | 1 | 29,772 | 0 | yes | none | FONT |
| font_italic | 1 | 29,772 | 0 | yes | none | FONT |
| extension_file_functions | 1 | 29,772 | 0 | yes | EXTN, STRG | OPTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT |
| room_ui_flex | 1 | 30,924 | 0 | yes | UILR, STRG | OPTN, EXTN, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR |
| layer_filter | 1 | 49,612 | 0 | yes | SPRT, SHDR, FEDS, ROOM, TPAG, TGIN, STRG, TXTR | OPTN, EXTN, AGRP, BGND, PATH, SCPT, FONT, TMLN, OBJT, ACRV, SEQN, EMBI, PSEM, PSYS, CODE, VARI, FUNC, FEAT |
| room_filter_layer | 1 | 49,740 | 0 | yes | SPRT, SHDR, FEDS, ROOM, TPAG, TGIN, STRG, TXTR | OPTN, EXTN, AGRP, BGND, PATH, SCPT, FONT, TMLN, OBJT, ACRV, SEQN, EMBI, PSEM, PSYS, CODE, VARI, FUNC, FEAT |
| options_flags | 1 | 29,772 | 0 | yes | none | OPTN, STRG |
| audio_group_second | 0 | 29,900 | 0 | yes | SOND, AGRP, STRG | OPTN, EXTN, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ACRV, SEQN, ROOM, EMBI, PSEM, PSYS, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR |

### Findings (LTS 2026)

- 51 of 52 variants build. All 51 read and write back byte-identical with G3MLib 1.0.1, and G3MLib exits 0 for each. The 52nd, `seq_asset_track`, does not build (see below).
- Sprite collision kind, manual mask bounds and nine slice settings change SPRT only, without a size change.
- Tile H and V, premultiplied alpha and edge filtering leave the data file unchanged for this sprite; they affect texture packing only.
- A separate texture page adds a page (TXTR, TPAG, TGIN).
- Tile layers, path layers, asset layers and tile animation change ROOM or BGND as expected.
- Default texture format is BZ2 + QOI (same bytes as the base). PNG doubles TXTR here, QOI adds about 4.5 KB.
- Sound compression 0 embeds the WAV in AUDO; 1 and 2 embed compressed audio; 3 (streamed) leaves AUDO unchanged, so the audio is an external file next to the data file. G3MStudio must handle streamed sounds outside the data file.
- Sequences: a sprite track (GMGraphicTrack, two keyframes) changes SEQN and STRG. An instance track changes SEQN only. Two moments and two broadcast messages change SEQN and STRG (the event names are new strings). An audio track changes SEQN, plus SOND, STRG and AUDO because the variant also gives Sound1 a WAV.
- G3MLib failure, audio track on a sound without a file: the first run of `seq_audio_track` used the base Sound1, which has no audio file, so SOND was empty while the sequence still pointed at sound index 0. Igor still wrote a data file (exit 1), and G3MLib exited 1 on it with "Error: Script execution failed: Invalid value for resource ID of type GameMakerChunkSOND: 0 (there are only 0) at 000074CC". No copy was written. The file is not a valid game, but the message does not say which chunk holds the bad reference. The variant now writes a WAV first and reads and writes cleanly.
- GMAssetTrack: Igor rejects it. The project fails to load with "Unrecognised type tag seen when parsing slot of type YoYoStudio.Resources.GMBaseTrack" at the track entry, so no data file is written and G3MLib never sees one. The keyframe stores found in CoreResources.dll are AssetInstance, AssetParticleSystem, AssetSequence, AssetSprite and AssetText; none is named for a GMAssetTrack. A fixture for that track needs a file saved by the IDE or a project of another version; it could not be generated by hand.
- Particle system with two emitters of different shapes changes PSEM and PSYS only.
- Fonts: the glyph tables in the .yy are used as given. Bold and italic change FONT content only (same size). SDF changes SHDR and STRG: the data file grows from 29,772 to 69,708 bytes. An extra range 160 to 255 gives a byte-identical data file, because the .yy glyph table has no entries for those characters and Igor does not regenerate glyphs from `ranges`. A font with real extra characters needs a glyph table generated by the IDE; no data was faked here.
- Extension with one GML file and two functions changes EXTN and STRG.
- Room UI with nested flex panels (a RoomUI resource, three panel levels, one instance) adds a UILR chunk and changes STRG. ROOM is unchanged.
- Filters: a vignette filter on a layer and a vignette filter effect layer both change SPRT, SHDR, FEDS, ROOM, TPAG, TGIN, STRG and TXTR. The built-in filter brings its own shader and texture, so the data file grows from 29,772 to about 49.6 KB.
- Game options: window colour, draw colour, game speed, scale, vsync, interpolation, resize, fullscreen, borderless and cursor flags change OPTN content and STRG only.
- A second audio group with Sound1 assigned to it changes SOND, AGRP and STRG. Whether the sound moves to a separate audio group file was not checked.

## LTS 2022 (runtime 2022.0.3.99)

Setup differences from the 2026 run:

- The runtime's own BaseProject is in the old 2.2 format (`modelName`, `mvc` style ids), so a 2.3 format project (`resourceVersion` 1.x, `resourceType` keys) is written by hand with `lts2022/make_lts2022_base.py`: Object1 with a Create event, Room1 (instance layer, background layer, 8 views), Sprite1 (64x64, one frame), TileSet1 (16x16 tiles from Sprite1), Path1 (two points), Sound1 (WAV), Script1, Font1, Timeline1, Shader1. Igor accepted it on the first try, with no options files.
- Igor of this runtime has no `Compile` command (windows commands: Run, PackageZip, PackageNsis, Package, Clean, Stop and the step hooks). `PackageZip` writes `data.win` next to the exe into the output folder and then exits 1 because its final zip move has an empty file name. The data file is complete, so exit 1 is expected for every row. Run with `--command PackageZip --lts2022`.
- Sound1 has a real WAV in the base and uses compression 0.
- The `room_*_layer` variants write 2026 field names; the `lts22_room_*_layer` variants write the same layers with 2.3 field names (`resourceVersion` 1.0, `tags`, no `$GM` keys). `--lts2022` skips the 2026-only variants (sequences, fonts and so on), because the 2022 base has none of those assets, and runs the three `lts22_` variants. Without the flag the `lts22_` variants are skipped; `--only` runs any variant by name.

Columns are the same as above.

| Variant | Igor exit | Size | G3MLib read+write | Byte-identical | Size changed | Content only |
| --- | ---: | ---: | ---: | --- | --- | --- |
| base | 1 | 65,252 | 0 | yes | none | none |
| room_tile_layer | 1 | 65,380 | 0 | yes | ROOM, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, EMBI, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR, AUDO |
| room_path_layer | 1 | 65,380 | 0 | yes | ROOM, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, EMBI, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR, AUDO |
| room_asset_layer | 1 | 65,380 | 0 | yes | ROOM | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, EMBI, TPAG, TGIN, CODE, VARI, FUNC, FEAT, STRG, TXTR, AUDO |
| sprite_mask_precise | 1 | 65,764 | 0 | yes | SPRT | OPTN, SOND, AGRP, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ROOM, EMBI, TPAG, TGIN, CODE, VARI, FUNC, FEAT, STRG, TXTR, AUDO |
| sprite_mask_ellipse | 1 | 65,764 | 0 | yes | SPRT | OPTN, SOND, AGRP, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ROOM, EMBI, TPAG, TGIN, CODE, VARI, FUNC, FEAT, STRG, TXTR, AUDO |
| sprite_mask_manual | 1 | 65,252 | 0 | yes | none | SPRT |
| sprite_nineslice | 1 | 65,252 | 0 | yes | none | SPRT |
| sprite_tiled_premultiplied | 1 | 65,252 | 0 | yes | none | none |
| sprite_separate_page | 1 | 65,492 | 0 | yes | TGIN, STRG, TXTR | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ROOM, EMBI, TPAG, CODE, VARI, FUNC, FEAT, AUDO |
| tileset_animation | 1 | 65,380 | 0 | yes | BGND, STRG | OPTN, SOND, AGRP, SPRT, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ROOM, EMBI, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR, AUDO |
| events_all | 1 | 68,836 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| room_instance_view | 1 | 65,252 | 0 | yes | ROOM, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, EMBI, TPAG, TGIN, CODE, VARI, FUNC, FEAT |
| texture_png | 1 | 65,524 | 0 | yes | TXTR | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, ROOM, EMBI, TGIN, CODE, VARI, FUNC, FEAT, STRG, AUDO |
| texture_qoi | 1 | 65,188 | 0 | yes | TXTR | AUDO |
| texture_bz2 | 1 | 65,252 | 0 | yes | none | none |
| event_destroy | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_alarm0 | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_step | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_step_begin | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_step_end | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_keyboard_left | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_mouse_left_down | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_other_room_start | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_other_user0 | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_async_http | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_draw | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_draw_gui | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_keypress_space | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_keyrelease_space | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_cleanup | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| event_gesture_tap | 1 | 65,508 | 0 | yes | OBJT, CODE, VARI, FUNC, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, ROOM, EMBI, TPAG, TGIN, FEAT, TXTR, AUDO |
| sound_c0 | 1 | 65,252 | 0 | yes | none | none |
| sound_c1 | 1 | 25,273 | 0 | yes | AUDO | SOND |
| sound_c2 | 1 | 25,273 | 0 | yes | AUDO | SOND |
| sound_c3 | 1 | 21,100 | 0 | yes | AUDO | SOND, STRG |
| lts22_room_tile_layer | 1 | 65,380 | 0 | yes | ROOM, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, EMBI, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR, AUDO |
| lts22_room_path_layer | 1 | 65,380 | 0 | yes | ROOM, STRG | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, EMBI, TPAG, TGIN, CODE, VARI, FUNC, FEAT, TXTR, AUDO |
| lts22_room_asset_layer | 1 | 65,380 | 0 | yes | ROOM | OPTN, SOND, AGRP, SPRT, BGND, PATH, SCPT, SHDR, FONT, TMLN, OBJT, EMBI, TPAG, TGIN, CODE, VARI, FUNC, FEAT, STRG, TXTR, AUDO |

### Findings (LTS 2022)

- All 39 variants build and read and write back byte-identical with G3MLib 1.0.1. No variant fails.
- The 2.3 field name layer variants produce the same ROOM chunk bytes as the 2026-name variants (856, 808 and 888 bytes for tile, path and asset, same hashes). Igor 2022 reads both spellings, so the earlier doubt about the 2026 names is settled: tile, path and asset layers were read with the right content.
- `sprite_tiled_premultiplied`, `texture_bz2` and `sound_c0` match the base build, as in the 2026 run (sound_c0 is the base setting). The texture format option changes TXTR in 2022 as it does in 2026.
- G3MTool `info` for the base build (65,252 bytes): Version GM 2022.0 (raw: 2023.6.0.0), bytecode 17. The FONT layout is that of 2023.6 and no non-LTS marker is present, so G3MLib sets the LTS branch. This matches the Deltarune label in the format notes (question 5); no special handling is needed to round-trip it.

## Project import

Checked 2026-10-26 on `l2/interop`. Method: Igor built a copy of the project (`Compile`, runtime 2026.0.0.23, `--pf` prefab folder). The test `YyCorpusTests.ImportedProjectExportsLikeGameMakersOwnBuild` (needs a `yy-accept.txt` next to the test assembly: project folder, built file, output folder) removes same-named resources from the fixture `data-2026.win`, imports the project's sprites (one repack), sounds, scripts, objects and rooms, compares sprites and rooms in memory, and exports both files for a diff. Igor exit 1 on TEST is the known missing `Sound1` audio file; the data file is still written.

TEST (1 sprite, 1 script, 1 object, 1 room; Sound1 has no audio, so GameMaker did not build it): after the fixes below, sprite size, margins, origin and frame pixels are equal, the object event, script entries and the room match. Not matched: `Sprite1` frame is stored as a 1x1 crop by GameMaker and as 64x64 by G3MLib (format-legal, decoded pixels are equal).

Cover Assault (160 sprites, 3 rooms, objects, scripts; read 5.4 s, one repack 22 s for all 160 sprites):

| Area | Result |
| --- | --- |
| Rooms | rm_init, rm_main_menu equal in header and layers (ids, depth, effects). rm_level_city has 23 instances, GameMaker's build 22 (not investigated). Particle system graphics in asset layers are skipped and reported. |
| Objects | Two objects fail to compile (OI-060), so no code of the batch imported. |
| Sprites | Size, margins, origin and mask type equal for all 160 (ellipse and diamond kinds map to precise masks). Frames equal except the colour of fully transparent pixels (format-legal) and one 3840x2160 sprite (OI-059). Rectangle sprites store no mask in GameMaker; fixed. 42 precise or ellipse masks differ by edge pixels (OI-059). |

Differences found and fixed in the library: event actions use library 1, id 603, kind 7, execute type 2, apply to self and an empty name (were zero with the code name); the script asset (`Script1`) is added next to `gml_Script_Script1`; rooms use speed 0, background colour `FF000000`, no draw, zero bounds, no legacy backgrounds, layer ids from 0 per room, effect enabled and type on every layer; rectangle sprites keep no collision mask; a missing `RoomCreationCode.gml` is tolerated.

Not run: LTS 2022 TEST22 (the base project script was not run in this session); sounds (TEST has no audio, Cover Assault sounds were imported but the audio comparison was not written); the built file was not loaded in the GameMaker runner.

## Not covered yet

YYC output, extensions with a native DLL, a real GMAssetTrack, fonts with glyph tables regenerated for extra ranges, and the audio group file.
