"""Build GameMaker feature fixtures and check how G3MLib handles each one.

Each variant copies a base project, changes one feature, builds it with Igor,
then records the chunk sizes, the G3MTool info summary and whether a G3MLib
read and write returns the same bytes. Results go to a JSON file and a
Markdown table.
"""

import argparse
import copy
import hashlib
import json
import shutil
import struct
import subprocess
import time
from pathlib import Path

from gmfixtures_new import (LTS22_LAYERS, variant_audio_group, variant_extension, variant_filter_layer, variant_font,
                            variant_layer_filter, variant_lts22_layer, variant_options, variant_particles,
                            variant_sequence, variant_ui_layer)
from yyfile import add_room_layer, load_yy, save_yy, variant_sound


def chunk_digests(data_file: Path) -> dict:
    """Map each chunk name to its size and a short content hash."""
    raw = data_file.read_bytes()
    if raw[:4] != b"FORM":
        raise ValueError(f"{data_file} is not a FORM file")
    chunks, offset = {}, 8
    while offset + 8 <= len(raw):
        name = raw[offset:offset + 4].decode("ascii", "replace")
        (size,) = struct.unpack_from("<I", raw, offset + 4)
        body = raw[offset + 8:offset + 8 + size]
        chunks[name] = f"{size}:{hashlib.sha1(body).hexdigest()[:12]}"
        offset += 8 + size
    return chunks


# Variants. Each function receives the project folder and the project name.

def add_event(project: Path, name: str, event_type: int, event_num: int, code_file: str) -> None:
    obj_path = project / "objects" / "Object1" / "Object1.yy"
    obj = load_yy(obj_path)
    obj["eventList"].append({
        "$GMEvent": "v1", "%Name": "", "collisionObjectId": None,
        "eventNum": event_num, "eventType": event_type, "isDnD": False,
        "name": "", "resourceType": "GMEvent", "resourceVersion": "2.0",
    })
    save_yy(obj_path, obj)
    (project / "objects" / "Object1" / code_file).write_text(f"// {name}\nvalue_{event_type}_{event_num} = 1;\n", encoding="utf-8")


EVENTS = [
    ("destroy", 1, 0, "Destroy_0.gml"),
    ("alarm0", 2, 0, "Alarm_0.gml"),
    ("step", 3, 0, "Step_0.gml"),
    ("step_begin", 3, 1, "Step_1.gml"),
    ("step_end", 3, 2, "Step_2.gml"),
    ("keyboard_left", 5, 37, "Keyboard_37.gml"),
    ("mouse_left_down", 6, 0, "Mouse_0.gml"),
    ("other_room_start", 7, 4, "Other_4.gml"),
    ("other_user0", 7, 10, "Other_10.gml"),
    ("async_http", 7, 62, "Other_62.gml"),
    ("draw", 8, 0, "Draw_0.gml"),
    ("draw_gui", 8, 64, "Draw_64.gml"),
    ("keypress_space", 9, 32, "KeyPress_32.gml"),
    ("keyrelease_space", 10, 32, "KeyRelease_32.gml"),
    ("cleanup", 12, 0, "CleanUp_0.gml"),
    ("gesture_tap", 13, 0, "Gesture_0.gml"),
]


def variant_event(entry):
    name, event_type, event_num, code_file = entry
    return lambda project, _: add_event(project, name, event_type, event_num, code_file)


def variant_all_events(project: Path, _: str) -> None:
    for entry in EVENTS:
        add_event(project, *entry)


def variant_texture_format(fmt: str):
    def apply(project: Path, project_name: str) -> None:
        yyp_path = project / f"{project_name}.yyp"
        yyp = load_yy(yyp_path)
        for group in yyp["TextureGroups"]:
            group["compressFormat"] = fmt
        save_yy(yyp_path, yyp)
    return apply


def variant_room_content(project: Path, _: str) -> None:
    room_path = project / "rooms" / "Room1" / "Room1.yy"
    room = load_yy(room_path)
    instance_layer = room["layers"][0]
    instance_layer["instances"].append({
        "$GMRInstance": "v4", "%Name": "inst_fixture", "colour": 4294967295,
        "frozen": False, "hasCreationCode": False, "ignore": False, "imageIndex": 0,
        "imageSpeed": 1.0, "inheritCode": False, "inheritedItemId": None,
        "inheritItemSettings": False, "isDnd": False, "name": "inst_fixture",
        "objectId": {"name": "Object1", "path": "objects/Object1/Object1.yy"},
        "properties": [], "resourceType": "GMRInstance", "resourceVersion": "2.0",
        "rotation": 0.0, "scaleX": 2.0, "scaleY": 1.5, "x": 100.0, "y": 200.0,
    })
    room["instanceCreationOrder"].append({"name": "inst_fixture", "path": "rooms/Room1/Room1.yy"})
    room["views"][0]["visible"] = True
    room["viewSettings"] = dict(room.get("viewSettings", {}), enableViews=True)
    save_yy(room_path, room)


def variant_tile_layer(project: Path, _: str) -> None:
    # 4 x 2 tiles; values are tile indices into TileSet1 (0 is empty).
    add_room_layer(project, {
        "$GMRTileLayer": "", "%Name": "Tiles_fixture", "name": "Tiles_fixture", "depth": 50,
        "resourceType": "GMRTileLayer", "resourceVersion": "2.1", "x": 0, "y": 0,
        "tilesetId": {"name": "TileSet1", "path": "tilesets/TileSet1/TileSet1.yy"},
        "tiles": {"SerialiseWidth": 4, "SerialiseHeight": 2,
                  "TileSerialiseData": [1, 2, 3, 0, 5, 6, 7, 8]},
    })


def variant_path_layer(project: Path, _: str) -> None:
    add_room_layer(project, {
        "$GMRPathLayer": "", "%Name": "Path_fixture", "name": "Path_fixture", "depth": 60,
        "colour": 4278190335, "resourceType": "GMRPathLayer", "resourceVersion": "2.0",
        "pathId": {"name": "Path1", "path": "paths/Path1/Path1.yy"},
    })


def variant_asset_layer(project: Path, _: str) -> None:
    add_room_layer(project, {
        "$GMRAssetLayer": "", "%Name": "Assets_fixture", "name": "Assets_fixture", "depth": 70,
        "resourceType": "GMRAssetLayer", "resourceVersion": "2.0",
        "assets": [{"$GMRSpriteGraphic": "v1", "%Name": "graphic_fixture", "name": "graphic_fixture",
                    "animationSpeed": 1.0, "colour": 4294967295, "frozen": False, "headPosition": 0.0,
                    "ignore": False, "inheritedItemId": None, "inheritItemSettings": False,
                    "resourceType": "GMRSpriteGraphic", "resourceVersion": "2.0", "rotation": 15.0,
                    "scaleX": 1.0, "scaleY": 1.0,
                    "spriteId": {"name": "Sprite1", "path": "sprites/Sprite1/Sprite1.yy"},
                    "x": 300.0, "y": 120.0}],
    })


def variant_sprite(**changes):
    def apply(project: Path, _: str) -> None:
        sprite_path = project / "sprites" / "Sprite1" / "Sprite1.yy"
        sprite = load_yy(sprite_path)
        for key, value in changes.items():
            if key == "nineSlice":
                sprite["nineSlice"].update(value)
            else:
                sprite[key] = value
        save_yy(sprite_path, sprite)
    return apply


def variant_tile_animation(project: Path, _: str) -> None:
    tileset_path = project / "tilesets" / "TileSet1" / "TileSet1.yy"
    tileset = load_yy(tileset_path)
    tileset["tileAnimationFrames"] = [{"$GMTileAnimation": "", "%Name": "animation_1", "frames": [1, 2, 3, 4],
                                       "name": "animation_1", "resourceType": "GMTileAnimation", "resourceVersion": "2.0"}]
    tileset["tileAnimationSpeed"] = 8.0
    save_yy(tileset_path, tileset)


VARIANTS = [("base", "unchanged base project", lambda project, name: None),
            ("room_tile_layer", "tile layer using TileSet1", variant_tile_layer),
            ("room_path_layer", "path layer showing Path1", variant_path_layer),
            ("room_asset_layer", "asset layer with a rotated sprite", variant_asset_layer),
            ("sprite_mask_precise", "precise per-frame collision mask", variant_sprite(collisionKind=0)),
            ("sprite_mask_ellipse", "ellipse collision mask", variant_sprite(collisionKind=2)),
            ("sprite_mask_manual", "manual mask bounds", variant_sprite(bboxMode=2, bbox_left=4, bbox_top=4, bbox_right=40, bbox_bottom=50)),
            ("sprite_nineslice", "nine slice enabled", variant_sprite(nineSlice={"enabled": True, "left": 8, "top": 8, "right": 8, "bottom": 8})),
            ("sprite_tiled_premultiplied", "tile H and V, premultiplied alpha, edge filtering", variant_sprite(HTile=True, VTile=True, preMultiplyAlpha=True, edgeFiltering=True)),
            ("sprite_separate_page", "separate texture page", variant_sprite(For3D=True)),
            ("tileset_animation", "tile animation of four frames", variant_tile_animation),
            ("events_all", "one of each common event type on Object1", variant_all_events),
            ("room_instance_view", "one scaled instance and view 0 enabled", variant_room_content),
            ("texture_png", "texture groups stored as PNG", variant_texture_format("png")),
            ("texture_qoi", "texture groups stored as QOI", variant_texture_format("qoi")),
            ("texture_bz2", "texture groups stored as BZ2 + QOI", variant_texture_format("bz2"))]
VARIANTS += [(f"event_{e[0]}", f"event {e[0]} only", variant_event(e)) for e in EVENTS]
VARIANTS += [(f"sound_c{c}", f"WAV sound, compression mode {c}", variant_sound(c)) for c in range(4)]


# Variants added in the second round. Field names follow the 2026 project format
# unless the name starts with "lts22_" (2.3 format, for the LTS 2022 run).

NEW_2026 = [
    ("seq_sprite_track", "sequence with a sprite track and two keyframes", variant_sequence("sprite")),
    ("seq_instance_track", "sequence with an instance track", variant_sequence("instance")),
    ("seq_audio_track", "sequence with an audio track", variant_sequence("audio")),
    ("seq_asset_track", "sequence with a GMAssetTrack", variant_sequence("asset")),
    ("seq_moments_messages", "sequence with two moments and two broadcast messages", variant_sequence("events")),
    ("particle_two_emitters", "particle system with two emitters of different shapes", variant_particles),
    ("font_sdf", "font with SDF enabled", variant_font(usesSDF=True)),
    ("font_extra_range", "font with an extra range 160 to 255", variant_font(ranges=[{"lower": 160, "upper": 255}])),
    ("font_bold", "bold font", variant_font(bold=True)),
    ("font_italic", "italic font", variant_font(italic=True)),
    ("extension_file_functions", "extension with one GML file and two functions", variant_extension),
    ("room_ui_flex", "room UI layer with nested flex panels", variant_ui_layer),
    ("layer_filter", "vignette filter on the instance layer", variant_layer_filter),
    ("room_filter_layer", "filter effect layer with vignette", variant_filter_layer),
    ("options_flags", "window colour, scale, vsync and window flags", variant_options),
    ("audio_group_second", "Sound1 in a second audio group", variant_audio_group),
]
LTS22_ONLY = [(f"lts22_room_{k}_layer", f"{k} layer written with 2.3 field names", variant_lts22_layer(v))
              for k, v in LTS22_LAYERS.items()]
VARIANTS += NEW_2026 + LTS22_ONLY


def run(cmd, log_path: Path) -> int:
    with log_path.open("w", encoding="utf-8", errors="replace") as log:
        return subprocess.run(cmd, stdout=log, stderr=subprocess.STDOUT, check=False).returncode


def build_variant(args, name: str, apply) -> dict:
    project_name = next(args.base.glob("*.yyp")).stem
    work = args.work / name
    shutil.rmtree(work, ignore_errors=True)
    shutil.copytree(args.base, work / "project", ignore=shutil.ignore_patterns(".git"))
    apply(work / "project", project_name)
    out_dir = work / "out"
    out_dir.mkdir()
    igor = args.runtime / "bin" / "igor" / "windows" / "x64" / "Igor.exe"
    cmd = [str(igor), "-j=8", f"--project={work / 'project' / (project_name + '.yyp')}",
           f"--rp={args.runtime}", "--runtime=VM", f"--user={args.user}",
           f"--cache={work / 'cache'}", f"--temp={work / 'temp'}",
           f"--of={out_dir / (project_name + '.win')}"]
    if args.prefabs:
        cmd.append(f"--pf={args.prefabs}")
    cmd += ["--", "windows", args.command]
    started = time.perf_counter()
    igor_code = run(cmd, work / "igor.log")
    result = {"variant": name, "igor_exit": igor_code, "build_s": round(time.perf_counter() - started, 2)}
    built = sorted(out_dir.glob("*.win"))
    if not built:
        result["error"] = "no data file written"
        return result
    data_file = built[0]
    result["size"] = data_file.stat().st_size
    result["chunks"] = chunk_digests(data_file)
    noop = work / "noop.csx"
    noop.write_text("", encoding="utf-8")
    copy = work / "roundtrip.win"
    result["g3mlib_exit"] = run([str(args.g3mtool), "execute", str(noop), "--data", str(data_file),
                                 "--output", str(copy)], work / "g3mlib.log")
    result["roundtrip"] = copy.exists() and copy.read_bytes() == data_file.read_bytes()
    return result


def write_markdown(results: list, base_chunks: dict, path: Path) -> None:
    # GEN8 differs in every build (timestamp, random IDs), so it is left out of both columns.
    # A new string shifts absolute pointers, so "content only" is noisy once STRG grows.
    lines = ["| Variant | Igor exit | Size | G3MLib read+write | Byte-identical | Size changed | Content only |",
             "| --- | ---: | ---: | ---: | --- | --- | --- |"]
    for r in results:
        if "error" in r:
            lines.append(f"| {r['variant']} | {r['igor_exit']} | | | | {r['error']} | |")
            continue
        size_changed, content_only = [], []
        for chunk, digest in r["chunks"].items():
            base = base_chunks.get(chunk)
            if chunk == "GEN8" or digest == base:
                continue
            same_size = base is not None and base.split(":")[0] == digest.split(":")[0]
            (content_only if same_size else size_changed).append(chunk)
        lines.append(f"| {r['variant']} | {r['igor_exit']} | {r['size']:,} | {r['g3mlib_exit']} | "
                     f"{'yes' if r['roundtrip'] else 'NO'} | {', '.join(size_changed) or 'none'} | "
                     f"{', '.join(content_only) or 'none'} |")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", type=Path, required=True, help="base GameMaker project folder")
    parser.add_argument("--work", type=Path, required=True, help="scratch folder for copies and builds")
    parser.add_argument("--runtime", type=Path, required=True, help="GameMaker runtime folder")
    parser.add_argument("--user", type=Path, required=True, help="GameMaker user folder with the licence")
    parser.add_argument("--prefabs", type=Path, help="GameMaker prefab library folder")
    parser.add_argument("--g3mtool", type=Path, required=True, help="G3MTool CLI executable")
    parser.add_argument("--out", type=Path, required=True, help="results folder (JSON and Markdown)")
    parser.add_argument("--command", default="Compile", help="Igor windows worker command (LTS 2022 has no Compile; use PackageZip)")
    parser.add_argument("--only", nargs="*", help="variant names to run")
    parser.add_argument("--lts2022", action="store_true", help="LTS 2022 set: the 2.3 layer variants instead of the 2026-only variants")
    args = parser.parse_args()

    skipped = {v[0] for v in (NEW_2026 if args.lts2022 else LTS22_ONLY)}
    selected = [v for v in VARIANTS if (v[0] in args.only if args.only else v[0] not in skipped)]
    results = []
    for name, description, apply in selected:
        try:
            result = build_variant(args, name, apply)
        except (OSError, ValueError, KeyError, StopIteration) as error:
            result = {"variant": name, "igor_exit": None, "error": f"{type(error).__name__}: {error}"}
        result["description"] = description
        results.append(result)
        print(json.dumps({k: v for k, v in result.items() if k != "chunks"}), flush=True)

    args.out.mkdir(parents=True, exist_ok=True)
    (args.out / "results.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    base = next((r for r in results if r["variant"] == "base" and "chunks" in r), None)
    write_markdown(results, base["chunks"] if base else {}, args.out / "results.md")


if __name__ == "__main__":
    main()
