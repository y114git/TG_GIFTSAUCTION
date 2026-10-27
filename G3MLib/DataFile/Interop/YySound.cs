using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using G3MLib.DataFile.Models;

namespace G3MLib.DataFile.Interop;

/// <summary>
/// Converts a GameMaker sound resource (<c>.yy</c> plus its audio file) to and from <see cref="GameMakerSound"/>.
/// </summary>
/// <remarks>
/// No transcoding: compressed and decompress-on-load sounds need an already compressed source file (ogg or mp3).
/// Sounds in audio groups other than the default one are not supported, their audio lives in separate group files.
/// </remarks>
public static class YySound
{
    private const int Uncompressed = 0, Compressed = 1, UncompressedOnLoad = 2, Streamed = 3;

    /// <summary>
    /// Reads a sound and stores its audio in the data file (not for streamed sounds, which stay external files).
    /// A sound with the same name is overwritten in place.
    /// </summary>
    public static GameMakerSound Read(string yyPath, GameMakerData data)
    {
        JsonElement root = YyFile.Parse(yyPath);
        string name = root.Str("name") ?? throw new InvalidDataException($"{yyPath}: sound has no name.");
        string file = root.Str("soundFile") ?? "";
        string audioPath = file.Length == 0 ? null : Path.Combine(Path.GetDirectoryName(yyPath), file);
        int compression = root.Int("compression");
        bool lossless = file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
        if (audioPath is not null && compression != Uncompressed && compression != Streamed && lossless)
            throw new NotSupportedException($"Sound \"{name}\" is compressed in the project but its source is a wav file, and transcoding is not supported.");

        string groupName = root.Child("audioGroupId").Str("name") ?? "audiogroup_default";
        int groupId = data.AudioGroups?.IndexOfName(groupName) ?? data.GetBuiltinSoundGroupID();
        if (groupId < 0)
            throw new InvalidDataException($"{yyPath}: audio group \"{groupName}\" is not in the data file.");
        if (groupId != data.GetBuiltinSoundGroupID())
            throw new NotSupportedException($"Sound \"{name}\" is in audio group \"{groupName}\", which is stored in a separate file.");

        GameMakerSound sound = data.Sounds.ByName(name);
        if (sound is null)
        {
            sound = new() { Name = data.Strings.MakeString(name) };
            data.Sounds.Add(sound);
        }

        sound.File = data.Strings.MakeString(file);
        sound.Type = data.Strings.MakeString(Path.GetExtension(file));
        sound.Volume = root.Float("volume", 1);
        sound.Preload = root.Bool("preload");
        sound.AudioLength = root.Float("duration");
        sound.GroupID = groupId;
        sound.Flags = compression switch
        {
            Streamed => GameMakerSound.AudioEntryFlags.Regular,
            UncompressedOnLoad => GameMakerSound.AudioEntryFlags.IsEmbedded | GameMakerSound.AudioEntryFlags.IsCompressed | GameMakerSound.AudioEntryFlags.IsDecompressedOnLoad | GameMakerSound.AudioEntryFlags.Regular,
            Compressed => GameMakerSound.AudioEntryFlags.IsCompressed | GameMakerSound.AudioEntryFlags.Regular,
            _ => GameMakerSound.AudioEntryFlags.IsEmbedded | GameMakerSound.AudioEntryFlags.Regular,
        };

        if (compression == Streamed || audioPath is null)
            return sound;
        GameMakerEmbeddedAudio audio = sound.AudioFile;
        if (audio is null)
        {
            audio = new();
            sound.AudioFile = audio;
            sound.AudioID = data.EmbeddedAudio.Count;
            data.EmbeddedAudio.Add(audio);
        }
        audio.Data = File.ReadAllBytes(audioPath);
        return sound;
    }

    /// <summary>
    /// Writes <paramref name="sound"/> as <c>{folder}/{name}.yy</c> and its embedded audio next to it.
    /// A streamed sound has no embedded audio: <c>soundFile</c> names the external file, which the caller copies.
    /// </summary>
    /// <param name="parent">Folder name shown in the asset browser (LTS 2022), or the project name (LTS 2026).</param>
    public static void Write(GameMakerSound sound, GameMakerData data, string folder, YyFormat format, string parent = "Sounds")
    {
        string name = sound.Name.Content;
        GameMakerSound.AudioEntryFlags flags = sound.Flags;
        int compression = flags == GameMakerSound.AudioEntryFlags.Regular ? Streamed
            : flags.HasFlag(GameMakerSound.AudioEntryFlags.IsDecompressedOnLoad) ? UncompressedOnLoad
            : flags.HasFlag(GameMakerSound.AudioEntryFlags.IsCompressed) ? Compressed
            : Uncompressed;

        string file = Path.GetFileName(sound.File?.Content ?? "");
        Directory.CreateDirectory(folder);
        if (sound.AudioFile is { Data: { } bytes })
        {
            if (file.Length == 0)
                file = name + (compression == Uncompressed ? ".wav" : ".ogg");
            File.WriteAllBytes(Path.Combine(folder, file), bytes);
        }

        string group = data.AudioGroups is { } groups && (uint)sound.GroupID < groups.Count ? groups[sound.GroupID].Name.Content : "audiogroup_default";
        JsonObject root = YyFile.Resource(format, "GMSound", name, "1.0", "v2");
        root["conversionMode"] = 0;
        root["compression"] = compression;
        root["volume"] = (double)sound.Volume;
        root["preload"] = sound.Preload;
        root["sampleRate"] = 44100;
        root["bitDepth"] = 1;
        if (format == YyFormat.Lts2026)
        {
            root["channelFormat"] = 0;
            root["compressionQuality"] = 4;
            root["exportDir"] = "";
        }
        else
        {
            root["bitRate"] = 128;
            root["type"] = 0;
        }
        root["audioGroupId"] = new JsonObject { ["name"] = group, ["path"] = $"audiogroups/{group}" };
        root["soundFile"] = file;
        root["duration"] = (double)sound.AudioLength;
        root["parent"] = format == YyFormat.Lts2026
            ? new JsonObject { ["name"] = parent, ["path"] = parent + ".yyp" }
            : new JsonObject { ["name"] = parent, ["path"] = $"folders/{parent}.yy" };
        if (format == YyFormat.Lts2022)
            root["tags"] = new JsonArray();
        YyFile.Write(Path.Combine(folder, name + ".yy"), root);
    }
}
