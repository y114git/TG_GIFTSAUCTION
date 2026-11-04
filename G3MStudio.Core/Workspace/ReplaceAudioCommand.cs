using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>What the bytes of an audio file announce, and how long the sound plays. Only WAV and OGG Vorbis are measured; nothing else is guessed.</summary>
public static class SoundAudio
{
    /// <summary>"wav", "ogg", "mp3", or "bin" when the bytes announce nothing known.</summary>
    public static string Format(byte[] bytes) => bytes switch
    {
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', ..] => "wav",
        [(byte)'O', (byte)'g', (byte)'g', (byte)'S', ..] => "ogg",
        [0x49, 0x44, 0x33, ..] or [0xFF, ..] => "mp3",
        _ => "bin",
    };

    /// <summary>The length in seconds from the header of a WAV or the last page of an OGG Vorbis stream; null when it cannot be told.</summary>
    public static double? Seconds(byte[] bytes) => Format(bytes) switch
    {
        "wav" => WavSeconds(bytes),
        "ogg" => OggSeconds(bytes),
        _ => null,
    };

    private static double? WavSeconds(byte[] bytes)
    {
        if (bytes.Length < 12) return null;
        uint byteRate = 0;
        for (var at = 12; at + 8 <= bytes.Length;)
        {
            var size = BitConverter.ToUInt32(bytes, at + 4);
            var id = System.Text.Encoding.ASCII.GetString(bytes, at, 4);
            if (id == "fmt " && at + 20 <= bytes.Length) byteRate = BitConverter.ToUInt32(bytes, at + 16);
            else if (id == "data") return byteRate > 0 ? Math.Min(size, bytes.Length - at - 8) / (double)byteRate : null;
            at += 8 + (int)Math.Min(size + (size & 1), int.MaxValue - 8);
        }

        return null;
    }

    private static double? OggSeconds(byte[] bytes)
    {
        // The sample rate sits in the Vorbis identification header (after the page header and its segment table); the length is the granule position of the last page.
        var ident = bytes.AsSpan().IndexOf("\u0001vorbis"u8);
        if (ident < 0 || ident + 16 > bytes.Length) return null;
        var rate = BitConverter.ToUInt32(bytes, ident + 12);
        var last = bytes.AsSpan().LastIndexOf("OggS"u8);
        if (rate == 0 || last < 0 || last + 14 > bytes.Length) return null;
        var granule = BitConverter.ToInt64(bytes, last + 6);
        return granule > 0 ? granule / (double)rate : null;
    }
}

/// <summary>
/// Replaces the audio of an embedded sound with a WAV or OGG file of the same kind. The kind must match because the sound's flags (compressed or
/// not) describe the old audio; the play length is read from the new file. Undo puts back the old audio. The audio is stored in the journal.
/// With <c>changeKind</c> the sound may switch between WAV and OGG: its type, file name and compressed flag follow, as the library sets them for a new sound.
/// </summary>
public sealed class ReplaceAudioCommand(string sound, byte[] audio, bool changeKind = false) : EditCommand
{
    public const string Name = "replaceAudio";
    public const int MaxBytes = 64 * 1024 * 1024;

    private byte[]? _before;
    private float _beforeLength;
    private (GameMakerString? Type, GameMakerString? File, GameMakerSound.AudioEntryFlags Flags) _beforeKind;

    public override string Kind => Name;

    public override string Editor => "sounds";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["sound"] = sound, ["audio"] = Convert.ToBase64String(audio) }
        .Concat(changeKind ? [new KeyValuePair<string, string>("changeKind", "1")] : []).ToDictionary();

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Sounds", sound), new("EmbeddedAudio", null)];

    public static ReplaceAudioCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["sound"], Convert.FromBase64String(a["audio"]), a.ContainsKey("changeKind"));

    public override void Apply(GameMakerData data)
    {
        var target = data.Sounds.FirstOrDefault(s => s.Name.Content == sound) ?? throw new InvalidOperationException($"Sound '{sound}' does not exist.");
        var file = target.AudioFile ?? throw new InvalidOperationException($"'{sound}' keeps its audio outside the data file, so there is nothing here to replace.");
        if (audio.Length is 0 or > MaxBytes) throw new InvalidOperationException("The audio file is empty or larger than 64 MB.");
        var kind = SoundAudio.Format(audio);
        var current = SoundAudio.Format(file.Data);
        if (kind is not ("wav" or "ogg") || kind != current && !(changeKind && current is "wav" or "ogg")) throw new InvalidOperationException($"'{sound}' holds {current.ToUpperInvariant()} audio: replace it with a {(current is "wav" or "ogg" ? current.ToUpperInvariant() : "WAV or OGG")} file.");
        var seconds = SoundAudio.Seconds(audio); // read first: nothing changes when the header is odd
        (_before, _beforeLength) = (file.Data, target.AudioLength);
        _beforeKind = (target.Type, target.File, target.Flags);
        if (kind != current)
        {
            var stem = Path.GetFileNameWithoutExtension(target.File?.Content ?? sound);
            target.Type = data.Strings.MakeString("." + kind);
            target.File = data.Strings.MakeString(stem + "." + kind);
            target.Flags = kind == "ogg" ? target.Flags | GameMakerSound.AudioEntryFlags.IsCompressed : target.Flags & ~GameMakerSound.AudioEntryFlags.IsCompressed;
        }

        file.Data = audio;
        if (seconds is { } length) target.AudioLength = (float)length;
    }

    public override void Revert(GameMakerData data)
    {
        var target = data.Sounds.FirstOrDefault(s => s.Name.Content == sound) ?? throw new InvalidOperationException($"Sound '{sound}' does not exist.");
        if (_before is null || target.AudioFile is not { } file) throw new InvalidOperationException("The audio was never replaced.");
        file.Data = _before;
        target.AudioLength = _beforeLength;
        // A sound that had no type or file name keeps the new one: the model does not take null there.
        if (_beforeKind.Type is { } kindType) target.Type = kindType;
        if (_beforeKind.File is { } kindFile) target.File = kindFile;
        target.Flags = _beforeKind.Flags;
    }
}
