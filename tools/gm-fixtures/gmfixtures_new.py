"""Fixture variants for the sequence, particle, font, extension, UI, filter, option and LTS 2022 layer features."""

import copy
import uuid
from pathlib import Path

from yyfile import add_resource, add_room_layer, load_yy, save_yy, variant_sound


def keyframe(store: str, key: float, length: float, channels: dict) -> dict:
    return {f"$Keyframe<{store}>": "", "Channels": channels, "Disabled": False, "id": str(uuid.uuid4()),
            "IsCreationKey": False, "Key": key, "Length": length, "resourceType": f"Keyframe<{store}>",
            "resourceVersion": "2.0", "Stretch": False}


def channel(kind: str, **fields) -> dict:
    return {f"${kind}": "", **fields, "resourceType": kind, "resourceVersion": "2.0"}


def track(kind: str, name: str, store: str, keyframes: list) -> dict:
    return {f"${kind}": "", "%Name": name, "builtinName": 0, "events": [], "inheritsTrackColour": True,
            "interpolation": 1, "isCreationTrack": False,
            "keyframes": {f"$KeyframeStore<{store}>": "", "Keyframes": keyframes,
                          "resourceType": f"KeyframeStore<{store}>", "resourceVersion": "2.0"},
            "modifiers": [], "name": name, "resourceType": kind, "resourceVersion": "2.0",
            "trackColour": 4284500210, "tracks": [], "traits": 0}


def ref(kind: str, name: str) -> dict:
    return {"name": name, "path": f"{kind}/{name}/{name}.yy"}


def add_sequence(project: Path, project_name: str, name: str, tracks: list, moments=(), messages=()) -> None:
    template = load_yy(project / "sequences" / "Sequence1" / "Sequence1.yy")
    template.update({"name": name, "%Name": name, "tracks": tracks, "length": 60.0})
    for store, label, items in (("moments", "Moments", moments), ("events", "Message", messages)):
        kind = f"{label}EventKeyframe"
        template[store]["Keyframes"] = [
            keyframe(kind, at, 1.0, {"0": channel(kind, Events=[text])}) for at, text in items]
    folder = project / "sequences" / name
    folder.mkdir(exist_ok=True)
    save_yy(folder / f"{name}.yy", template)
    add_resource(project, project_name, "sequences", name)


# kind: track type, track name, keyframe type, keyframe times, keyframe length, asset folder, asset name, extra channel fields
SEQUENCE_TRACKS = {
    "sprite": ("GMGraphicTrack", "Sprite1", "AssetSpriteKeyframe", (0.0, 30.0), 30.0, "sprites", "Sprite1", {}),
    "instance": ("GMInstanceTrack", "Object1", "AssetInstanceKeyframe", (5.0,), 40.0, "objects", "Object1", {}),
    "audio": ("GMAudioTrack", "Sound1", "AudioKeyframe", (0.0,), 30.0, "sounds", "Sound1", {"Mode": 0}),
    "asset": ("GMAssetTrack", "Asset1", "AssetSpriteKeyframe", (0.0,), 30.0, "sprites", "Sprite1", {}),
}


def variant_sequence(kind: str):
    def apply(project: Path, project_name: str) -> None:
        moments, messages, tracks = (), (), []
        if kind == "audio":
            variant_sound(0)(project, project_name)  # the base Sound1 has no file, so SOND would be empty
        if kind in SEQUENCE_TRACKS:
            track_type, name, key_type, times, length, folder, asset, extra = SEQUENCE_TRACKS[kind]
            tracks = [track(track_type, name, key_type,
                            [keyframe(key_type, at, length, {"0": channel(key_type, Id=ref(folder, asset), **extra)})
                             for at in times])]
        else:
            moments = [(10.0, "moment_a"), (20.0, "moment_b")]
            messages = [(5.0, "message_a"), (25.0, "message_b")]
        add_sequence(project, project_name, f"Seq_{kind}", tracks, moments, messages)
    return apply

def variant_particles(project: Path, _: str) -> None:
    path = project / "particles" / "ParticleSystem1" / "ParticleSystem1.yy"
    system = load_yy(path)
    first = system["emitters"][0]
    second = copy.deepcopy(first)
    second.update({"%Name": "Emitter2", "name": "Emitter2", "shape": 2, "distribution": 1, "emitCount": 3,
                   "regionW": 128.0, "regionH": 32.0, "startColour": 4278190335, "endColour": 4294901760,
                   "speedMin": 1.0, "speedMax": 8.0, "additiveBlend": True})
    first["shape"] = 1
    system["emitters"].append(second)
    save_yy(path, system)


def variant_font(**changes):
    def apply(project: Path, _: str) -> None:
        path = project / "fonts" / "Font1" / "Font1.yy"
        font = load_yy(path)
        if "ranges" in changes:
            font["ranges"] = font["ranges"] + changes.pop("ranges")
        font.update(changes)
        save_yy(path, font)
    return apply


def variant_extension(project: Path, project_name: str) -> None:
    folder = project / "extensions" / "Extension1"
    (folder / "ext_funcs.gml").write_text(
        "function ext_add(a, b) { return a + b; }\nfunction ext_twice(a) { return a * 2; }\n", encoding="utf-8")
    path = folder / "Extension1.yy"
    ext = load_yy(path)
    functions = [{"$GMExtensionFunction": "", "%Name": n, "argCount": c, "args": [2] * c, "documentation": "",
                  "externalName": n, "help": "", "hidden": False, "kind": 11, "name": n,
                  "resourceType": "GMExtensionFunction", "resourceVersion": "2.0", "returnType": 2}
                 for n, c in (("ext_add", 2), ("ext_twice", 1))]
    ext["files"] = [{"$GMExtensionFile": "", "%Name": "ext_funcs.gml", "constants": [], "copyToTargets": -1,
                     "filename": "ext_funcs.gml", "final": "", "functions": functions, "init": "", "kind": 2,
                     "name": "ext_funcs.gml",
                     "order": [{"name": f["name"], "path": "extensions/Extension1/Extension1.yy"} for f in functions],
                     "origname": "", "ProxyFiles": [], "resourceType": "GMExtensionFile",
                     "resourceVersion": "2.0", "uncompress": False, "usesRunnerInterface": False}]
    save_yy(path, ext)


def flex_panel(name: str, children: list, width: dict, height: dict, direction: int = 0) -> dict:
    return {"$GMRFlexPanel": "", "%Name": name, "alignItems": 2, "children": children, "flexDirection": direction,
            "height": height, "justifyContent": 1, "name": name, "nodeColour": 4282935794,
            "resourceType": "GMRFlexPanel", "resourceVersion": "2.0", "width": width}


def variant_ui_layer(project: Path, project_name: str) -> None:
    def pct(v): return {"unit": 2, "value": v}
    def px(v): return {"unit": 3, "value": v}
    instance = {"$GMRInstance": "v4", "%Name": "inst_ui1", "colour": 4294967295,
                "flexProperties": {"$InstanceFlexProps": "v1", "anchor": 4, "resourceType": "InstanceFlexProps"},
                "frozen": False, "hasCreationCode": False, "ignore": False, "imageIndex": 0, "imageSpeed": 1.0,
                "inheritCode": False, "inheritedItemId": None, "inheritItemSettings": False, "isDnd": False,
                "name": "inst_ui1", "objectId": ref("objects", "Object1"), "properties": [],
                "resourceType": "GMRInstance", "resourceVersion": "2.0", "rotation": 0.0, "scaleX": 1.0,
                "scaleY": 1.0, "x": 0.0, "y": 0.0}
    inner_a = flex_panel("PanelA", [instance], px(100.0), pct(50.0))
    inner_b = flex_panel("PanelB", [flex_panel("PanelB1", [], pct(50.0), px(40.0), 1)], pct(40.0), pct(50.0), 1)
    layer = {"$GMRUILayer": "v1", "%Name": "UI_fixture", "alignItems": 2,
             "children": [flex_panel("Root", [inner_a, inner_b], pct(100.0), pct(100.0))],
             "flexDirection": 0, "gridX": 32, "gridY": 32, "justifyContent": 1, "name": "UI_fixture",
             "nodeColour": 4283058418, "resourceType": "GMRUILayer", "resourceVersion": "2.0"}
    folder = project / "roomui" / "RoomUI"
    folder.mkdir(parents=True)
    save_yy(folder / "RoomUI.yy", {
        "$GMRoomUI": "", "%Name": "RoomUI", "children": [layer], "locked": False, "name": "RoomUI",
        "parent": {"name": project_name, "path": f"{project_name}.yyp"}, "resourceType": "GMRoomUI",
        "resourceVersion": "2.0", "viewspaceChildren": [], "visible": True})
    add_resource(project, project_name, "roomui", "RoomUI")
    room_path = project / "rooms" / "Room1" / "Room1.yy"
    room = load_yy(room_path)
    room["instanceCreationOrder"].append({"name": "inst_ui1", "path": "roomui/RoomUI/RoomUI.yy"})
    save_yy(room_path, room)


VIGNETTE = [{"name": "g_VignetteEdges", "type": 0, "value": "2"},
            {"name": "g_VignetteSharpness", "type": 0, "value": "8"},
            {"name": "g_VignetteTexture", "type": 2, "value": "_filter_vignette_texture"}]


def variant_layer_filter(project: Path, _: str) -> None:
    room_path = project / "rooms" / "Room1" / "Room1.yy"
    room = load_yy(room_path)
    room["layers"][0].update({"effectType": "_filter_vignette", "properties": copy.deepcopy(VIGNETTE)})
    save_yy(room_path, room)


def variant_filter_layer(project: Path, _: str) -> None:
    add_room_layer(project, {
        "$GMREffectLayer": "", "%Name": "Vignette_fixture", "name": "Vignette_fixture", "depth": -100,
        "effectType": "_filter_vignette", "resourceType": "GMREffectLayer", "resourceVersion": "2.0",
        "properties": copy.deepcopy(VIGNETTE)})


def variant_options(project: Path, _: str) -> None:
    main_path = project / "options" / "main" / "options_main.yy"
    main = load_yy(main_path)
    main.update({"option_window_colour": 16744448, "option_draw_colour": 4278255360, "option_game_speed": 30})
    save_yy(main_path, main)
    win_path = project / "options" / "windows" / "options_windows.yy"
    win = load_yy(win_path)
    win.update({"option_windows_scale": 2, "option_windows_vsync": False, "option_windows_interpolate_pixels": False,
                "option_windows_resize_window": True, "option_windows_start_fullscreen": True,
                "option_windows_borderless": True, "option_windows_display_cursor": False})
    save_yy(win_path, win)


def variant_audio_group(project: Path, project_name: str) -> None:
    yyp_path = project / f"{project_name}.yyp"
    yyp = load_yy(yyp_path)
    yyp["AudioGroups"].append({"$GMAudioGroup": "v1", "%Name": "audiogroup_2", "exportDir": "",
                               "name": "audiogroup_2", "resourceType": "GMAudioGroup",
                               "resourceVersion": "2.0", "targets": -1})
    save_yy(yyp_path, yyp)
    variant_sound(0)(project, project_name)
    sound_path = project / "sounds" / "Sound1" / "Sound1.yy"
    sound = load_yy(sound_path)
    sound["audioGroupId"] = {"name": "audiogroup_2", "path": "audiogroups/audiogroup_2"}
    save_yy(sound_path, sound)


LTS22_COMMON = {"effectEnabled": True, "effectType": None, "gridX": 32, "gridY": 32, "hierarchyFrozen": False,
                "inheritLayerDepth": False, "inheritLayerSettings": False, "layers": [], "properties": [],
                "userdefinedDepth": False, "visible": True, "resourceVersion": "1.0", "tags": []}


def variant_lts22_layer(layer: dict):
    def apply(project: Path, _: str) -> None:
        room_path = project / "rooms" / "Room1" / "Room1.yy"
        room = load_yy(room_path)
        room["layers"].insert(1, dict(LTS22_COMMON, **layer))
        save_yy(room_path, room)
    return apply


LTS22_LAYERS = {
    "tile": {"name": "Tiles_fixture", "depth": 50, "resourceType": "GMRTileLayer", "x": 0, "y": 0,
             "tilesetId": ref("tilesets", "TileSet1"),
             "tiles": {"SerialiseWidth": 4, "SerialiseHeight": 2, "TileSerialiseData": [1, 2, 3, 0, 5, 6, 7, 8]}},
    "path": {"name": "Path_fixture", "depth": 60, "resourceType": "GMRPathLayer", "colour": 4278190335,
             "pathId": ref("paths", "Path1")},
    "asset": {"name": "Assets_fixture", "depth": 70, "resourceType": "GMRAssetLayer",
              "assets": [{"spriteId": ref("sprites", "Sprite1"), "headPosition": 0.0, "rotation": 15.0, "scaleX": 1.0,
                          "scaleY": 1.0, "animationSpeed": 1.0, "colour": 4294967295, "inheritedItemId": None,
                          "frozen": False, "ignore": False, "inheritItemSettings": False, "x": 300.0, "y": 120.0,
                          "resourceVersion": "1.0", "name": "graphic_fixture", "tags": [],
                          "resourceType": "GMRSpriteGraphic"}]},
}
