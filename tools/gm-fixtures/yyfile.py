"""Shared helpers for the fixture builder: reading and writing GameMaker .yy files and the common project edits."""

import json
import math
import re
import struct
import wave
from pathlib import Path

TRAILING_COMMA = re.compile(r",(\s*[}\]])")


def load_yy(path: Path) -> dict:
    # GameMaker writes JSON with trailing commas; strip them before parsing.
    return json.loads(TRAILING_COMMA.sub(r"\1", path.read_text(encoding="utf-8")))


def save_yy(path: Path, data: dict) -> None:
    path.write_text(json.dumps(data, indent=2), encoding="utf-8")



def write_sine_wav(path: Path, seconds: float = 0.5, rate: int = 44100) -> None:
    with wave.open(str(path), "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(rate)
        frames = bytearray()
        for i in range(int(seconds * rate)):
            frames += struct.pack("<h", int(8000 * math.sin(2 * math.pi * 440 * i / rate)))
        out.writeframes(bytes(frames))


def variant_sound(compression: int):
    def apply(project: Path, _: str) -> None:
        folder = project / "sounds" / "Sound1"
        write_sine_wav(folder / "Sound1.wav")
        sound_path = folder / "Sound1.yy"
        sound = load_yy(sound_path)
        sound["soundFile"] = "Sound1.wav"
        sound["compression"] = compression
        sound["duration"] = 0.5
        save_yy(sound_path, sound)
    return apply



LAYER_COMMON = {"effectEnabled": True, "effectType": None, "gridX": 16, "gridY": 16,
                "hierarchyFrozen": False, "inheritLayerDepth": False, "inheritLayerSettings": False,
                "inheritSubLayers": True, "inheritVisibility": True, "layers": [], "properties": [],
                "userdefinedDepth": True, "visible": True}


def add_room_layer(project: Path, layer: dict) -> None:
    room_path = project / "rooms" / "Room1" / "Room1.yy"
    room = load_yy(room_path)
    room["layers"].insert(1, dict(LAYER_COMMON, **layer))
    save_yy(room_path, room)



def add_resource(project: Path, project_name: str, kind: str, name: str) -> None:
    yyp_path = project / f"{project_name}.yyp"
    yyp = load_yy(yyp_path)
    yyp["resources"].append({"id": {"name": name, "path": f"{kind}/{name}/{name}.yy"}})
    save_yy(yyp_path, yyp)
