"""Write a GameMaker LTS 2022 (2.3 format) base project with one asset of every type.

Usage: python make_lts2022_base.py <project folder>; the folder should start as a copy of the
runtime's BaseProject. Shader sources are read from this script's folder."""
import json, struct, zlib, math, wave, shutil, uuid, sys
from pathlib import Path
HERE = Path(__file__).parent
P = Path(sys.argv[1])
for d in ("options", "folders", "sprites", "objects", "rooms", "tilesets", "paths", "sounds", "scripts", "timelines", "shaders", "fonts"):
    shutil.rmtree(P / d, ignore_errors=True)
for f in P.glob("*.yyp"):
    f.unlink()
def g(): return str(uuid.uuid4())
def ref(kind, name): return {"name": name, "path": f"{kind}/{name}/{name}.yy"}
def w(kind, name, data, ext="yy"):
    d = P / kind / name; d.mkdir(parents=True, exist_ok=True)
    (d / f"{name}.{ext}").write_text(json.dumps(data, indent=2), encoding="utf-8")
    return d
def png(path, wd, ht, rgba=(200, 60, 60, 255)):
    raw = (b"\0" + bytes(rgba) * wd) * ht
    def ch(t, b): return struct.pack(">I", len(b)) + t + b + struct.pack(">I", zlib.crc32(t + b))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + ch(b"IHDR", struct.pack(">IIBBBBB", wd, ht, 8, 6, 0, 0, 0)) + ch(b"IDAT", zlib.compress(raw)) + ch(b"IEND", b""))
def par(folder): return {"name": folder, "path": f"folders/{folder}.yy"}
V = {"resourceVersion": "1.0", "tags": []}
TG = {"name": "Default", "path": "texturegroups/Default"}

fid, lid = g(), g()
sref = ref("sprites", "Sprite1")
d = P / "sprites/Sprite1"
png(d / f"{fid}.png", 64, 64); png(d / "layers" / fid / f"{lid}.png", 64, 64)
w("sprites", "Sprite1", {
 "bboxMode": 0, "collisionKind": 1, "type": 0, "origin": 4, "preMultiplyAlpha": False, "edgeFiltering": False,
 "collisionTolerance": 0, "swfPrecision": 2.525, "bbox_left": 0, "bbox_right": 63, "bbox_top": 0, "bbox_bottom": 63,
 "HTile": False, "VTile": False, "For3D": False, "width": 64, "height": 64,
 "textureGroupId": TG, "swatchColours": None, "gridX": 0, "gridY": 0,
 "frames": [{"compositeImage": {"FrameId": {"name": fid, "path": sref["path"]}, "LayerId": None, **V, "name": "composite", "resourceType": "GMSpriteBitmap"},
   "images": [{"FrameId": {"name": fid, "path": sref["path"]}, "LayerId": {"name": lid, "path": sref["path"]}, **V, "name": "", "resourceType": "GMSpriteBitmap"}],
   "parent": sref, **V, "name": fid, "resourceType": "GMSpriteFrame"}],
 "sequence": {"spriteId": sref, "timeUnits": 1, "playback": 1, "playbackSpeed": 30.0, "playbackSpeedType": 0, "autoRecord": True, "volume": 1.0, "length": 1.0,
   "events": {"Keyframes": [], "resourceVersion": "1.0", "resourceType": "KeyframeStore<MessageEventKeyframe>"},
   "moments": {"Keyframes": [], "resourceVersion": "1.0", "resourceType": "KeyframeStore<MomentsEventKeyframe>"},
   "tracks": [{"name": "frames", "spriteId": None, "keyframes": {"Keyframes": [{"id": g(), "Key": 0.0, "Length": 1.0, "Stretch": False, "Disabled": False, "IsCreationKey": False,
      "Channels": {"0": {"Id": {"name": fid, "path": sref["path"]}, "resourceVersion": "1.0", "resourceType": "SpriteFrameKeyframe"}},
      "resourceVersion": "1.0", "resourceType": "Keyframe<SpriteFrameKeyframe>"}], "resourceVersion": "1.0", "resourceType": "KeyframeStore<SpriteFrameKeyframe>"},
     "trackColour": 0, "inheritsTrackColour": True, "builtinName": 0, "traits": 0, "interpolation": 1, "tags": [], "events": [], "isCreationTrack": False,
     "resourceVersion": "1.0", "resourceType": "GMSpriteFramesTrack", "modifiers": []}],
   "visibleRange": None, "lockOrigin": False, "showBackdrop": True, "showBackdropImage": False, "backdropImagePath": "", "backdropImageOpacity": 0.5,
   "backdropWidth": 1366, "backdropHeight": 768, "backdropXOffset": 0.0, "backdropYOffset": 0.0, "xorigin": 32, "yorigin": 32,
   "eventToFunction": {}, "eventStubScript": None, "parent": sref, "resourceVersion": "1.4", "name": "Sprite1", "tags": [], "resourceType": "GMSequence"},
 "layers": [{"visible": True, "isLocked": False, "blendMode": 0, "opacity": 100.0, "displayName": "default", **V, "name": lid, "resourceType": "GMImageLayer"}],
 "nineSlice": {"left": 0, "top": 0, "right": 0, "bottom": 0, "guideColour": [4294902015] * 4, "highlightColour": 1728023040, "highlightStyle": 0, "enabled": False, "tileMode": [0, 0, 0, 0, 0], "resourceVersion": "1.0", "resourceType": "GMNineSliceData"},
 "parent": par("Sprites"), **V, "name": "Sprite1", "resourceType": "GMSprite"})

od = w("objects", "Object1", {"spriteId": None, "solid": False, "visible": True, "spriteMaskId": None, "persistent": False, "parentObjectId": None,
 "physicsObject": False, "physicsSensor": False, "physicsShape": 1, "physicsGroup": 1, "physicsDensity": 0.5, "physicsRestitution": 0.1,
 "physicsLinearDamping": 0.1, "physicsAngularDamping": 0.1, "physicsFriction": 0.2, "physicsStartAwake": True, "physicsKinematic": False, "physicsShapePoints": [],
 "eventList": [{"isDnD": False, "eventNum": 0, "eventType": 0, "collisionObjectId": None, **V, "name": "", "resourceType": "GMEvent"}],
 "properties": [], "overriddenProperties": [], "parent": par("Objects"), **V, "name": "Object1", "resourceType": "GMObject"})
(od / "Create_0.gml").write_text("x = 0;\n")

view = {"inherit": False, "visible": False, "xview": 0, "yview": 0, "wview": 1366, "hview": 768, "xport": 0, "yport": 0, "wport": 1366, "hport": 768, "hborder": 32, "vborder": 32, "hspeed": -1, "vspeed": -1, "objectId": None}
lay = {"visible": True, "userdefinedDepth": False, "inheritLayerDepth": False, "inheritLayerSettings": False, "gridX": 32, "gridY": 32, "layers": [], "hierarchyFrozen": False, "effectEnabled": True, "effectType": None, "properties": []}
w("rooms", "Room1", {"isDnd": False, "volume": 1.0, "parentRoom": None, "views": [dict(view) for _ in range(8)],
 "layers": [dict(lay, instances=[], depth=0, **V, name="Instances", resourceType="GMRInstanceLayer"),
            dict(lay, spriteId=None, colour=4278190080, x=0, y=0, htiled=False, vtiled=False, hspeed=0.0, vspeed=0.0, stretch=False, animationFPS=15.0, animationSpeedType=0, userdefinedAnimFPS=False, depth=100, **V, name="Background", resourceType="GMRBackgroundLayer")],
 "inheritLayers": False, "creationCodeFile": "", "inheritCode": False, "instanceCreationOrder": [], "inheritCreationOrder": False, "sequenceId": None,
 "roomSettings": {"inheritRoomSettings": False, "Width": 1366, "Height": 768, "persistent": False},
 "viewSettings": {"inheritViewSettings": False, "enableViews": False, "clearViewBackground": False, "clearDisplayBuffer": True},
 "physicsSettings": {"inheritPhysicsSettings": False, "PhysicsWorld": False, "PhysicsWorldGravityX": 0.0, "PhysicsWorldGravityY": 10.0, "PhysicsWorldPixToMetres": 0.1},
 "parent": par("Rooms"), **V, "name": "Room1", "resourceType": "GMRoom"})

w("tilesets", "TileSet1", {"spriteId": sref, "tileWidth": 16, "tileHeight": 16, "tilexoff": 0, "tileyoff": 0, "tilehsep": 0, "tilevsep": 0,
 "spriteNoExport": False, "textureGroupId": TG, "out_tilehborder": 2, "out_tilevborder": 2,
 "out_columns": 0, "tile_count": 16, "autoTileSets": [], "tileAnimationFrames": [], "tileAnimationSpeed": 15.0, "tileAnimation": {"FrameData": [0], "SerialiseFrameCount": 1},
 "macroPageTiles": {"SerialiseWidth": 0, "SerialiseHeight": 0, "TileSerialiseData": []}, "parent": par("Tile Sets"), **V, "name": "TileSet1", "resourceType": "GMTileSet"})

w("paths", "Path1", {"kind": 0, "closed": False, "precision": 4, "points": [{"x": 0.0, "y": 0.0, "speed": 100.0}, {"x": 100.0, "y": 50.0, "speed": 100.0}],
 "parent": par("Paths"), **V, "name": "Path1", "resourceType": "GMPath"})

sd = w("sounds", "Sound1", {"conversionMode": 0, "compression": 0, "volume": 1.0, "preload": False, "bitRate": 128, "sampleRate": 44100, "type": 0, "bitDepth": 1,
 "audioGroupId": {"name": "audiogroup_default", "path": "audiogroups/audiogroup_default"}, "soundFile": "Sound1.wav", "duration": 0.5,
 "parent": par("Sounds"), **V, "name": "Sound1", "resourceType": "GMSound"})
with wave.open(str(sd / "Sound1.wav"), "wb") as o:
    o.setnchannels(1); o.setsampwidth(2); o.setframerate(44100)
    o.writeframes(b"".join(struct.pack("<h", int(8000 * math.sin(2 * math.pi * 440 * i / 44100))) for i in range(22050)))

sc = w("scripts", "Script1", {"isDnD": False, "isCompatibility": False, "parent": par("Scripts"), **V, "name": "Script1", "resourceType": "GMScript"})
(sc / "Script1.gml").write_text("function Script1() {\n    return 1;\n}\n")

td = w("timelines", "Timeline1", {"momentList": [{"moment": 0, "evnt": {"isDnD": False, "eventNum": 0, "eventType": 0, "collisionObjectId": None, **V, "name": "", "resourceType": "GMEvent"}, **V, "name": "", "resourceType": "GMMoment"}],
 "parent": par("Timelines"), **V, "name": "Timeline1", "resourceType": "GMTimeline"})
(td / "moment_0.gml").write_text("x = 1;\n")

shd = w("shaders", "Shader1", {"type": 1, "parent": par("Shaders"), **V, "name": "Shader1", "resourceType": "GMShader"})
for e in ("fsh", "vsh"):
    shutil.copy(HERE / f"Shader1.{e}", shd / f"Shader1.{e}")

w("fonts", "Font1", {"hinting": 0, "glyphOperations": 0, "interpreter": 0, "pointRounding": 0, "applyKerning": 0, "fontName": "Arial", "styleName": "Regular", "size": 12.0,
 "bold": False, "italic": False, "charset": 0, "AntiAlias": 1, "first": 0, "last": 127, "sampleText": "abcdef ABCDEF 0123456789",
 "includeTTF": False, "TTFName": "", "textureGroupId": TG, "ascenderOffset": 0, "ascender": 0,
 "ranges": [{"lower": 32, "upper": 127}], "regenerateBitmap": False, "canGenerateBitmap": True, "maintainGms1Font": False, "glyphs": {}, "kerningPairs": [],
 "parent": par("Fonts"), **V, "name": "Font1", "resourceType": "GMFont"})

res = [("fonts","Font1"),("objects","Object1"),("paths","Path1"),("rooms","Room1"),("scripts","Script1"),("shaders","Shader1"),("sounds","Sound1"),("sprites","Sprite1"),("tilesets","TileSet1"),("timelines","Timeline1")]
folders = ["Fonts", "Objects", "Paths", "Rooms", "Scripts", "Shaders", "Sounds", "Sprites", "Tile Sets", "Timelines"]
(P / "folders").mkdir(exist_ok=True)
for f in folders:
    (P / "folders" / f"{f}.yy").write_text(json.dumps({"folderPath": f"folders/{f}.yy", "order": 1, **V, "name": f, "resourceType": "GMFolder"}, indent=2))
yyp = {"resources": [{"id": ref(k, n)} for k, n in res], "Options": [], "isDnDProject": False, "isEcma": False, "LibraryEmitters": [], "configs": {"name": "Default", "children": []},
 "RoomOrderNodes": [{"roomId": ref("rooms", "Room1")}], "Folders": [{"folderPath": f"folders/{f}.yy", "order": i + 1, **V, "name": f, "resourceType": "GMFolder"} for i, f in enumerate(folders)],
 "AudioGroups": [{"targets": -1, "resourceVersion": "1.0", "name": "audiogroup_default", "resourceType": "GMAudioGroup"}],
 "TextureGroups": [{"isScaled": True, "autocrop": True, "border": 2, "mipsToGenerate": 0, "targets": -1, "groupParent": None, "resourceVersion": "1.3", "name": "Default", "resourceType": "GMTextureGroup"}],
 "IncludedFiles": [], "MetaData": {"IDEVersion": "2022.0.3.85"}, "defaultScriptType": 1, "templateType": None, "tutorial": "",
 "resourceVersion": "1.4", "name": "TEST22", "tags": [], "resourceType": "GMProject"}
(P / "TEST22.yyp").write_text(json.dumps(yyp, indent=2))
