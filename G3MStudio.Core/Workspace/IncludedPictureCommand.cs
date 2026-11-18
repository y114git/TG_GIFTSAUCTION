using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using ImageMagick;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Writes new bytes over a file in the Included Files folder. Unlike other included file changes it is one undoable step: the old bytes are kept and Undo writes them back.
/// The file goes straight to the game folder, so without <c>force</c> the command refuses with a warning. A subclass says which files it takes and how its content becomes bytes.
/// </summary>
public abstract class IncludedBytesCommand(string dataFile, string relative, byte[] given, bool force) : EditCommand
{
    private byte[]? _before;

    public override string Editor => "included";

    /// <summary>The journal name of the content (picture, text, audio).</summary>
    protected abstract string ArgName { get; }

    protected abstract bool Accepts(string path);

    /// <summary>The bytes to write for the given content; <paramref name="target"/> is the file's path, so the picture kind can follow its extension.</summary>
    protected abstract byte[] Encode(byte[] content, string target);

    protected abstract string RiskTitle { get; }

    protected abstract string RiskBody { get; }

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string> { ["dataFile"] = dataFile, ["path"] = relative, [ArgName] = Convert.ToBase64String(given) }
        .Concat(force ? [new KeyValuePair<string, string>("force", "1")] : []).ToDictionary();

    public override void Apply(GameMakerData data)
    {
        if (!force) throw new RiskyRefusalException(RiskTitle, RiskBody, $"'{relative}' is a loose file in the game folder; the new content is written to it at once.");
        var target = new IncludedFiles(dataFile).Resolve(relative);
        if (!Accepts(target) || !File.Exists(target)) throw new InvalidOperationException($"'{relative}' is not a file of this kind that exists.");
        var bytes = Encode(given, target); // first: nothing is touched when the content cannot be encoded
        _before ??= File.ReadAllBytes(target);
        Write(target, bytes);
    }

    public override void Revert(GameMakerData data) => Write(new IncludedFiles(dataFile).Resolve(relative), _before ?? throw new InvalidOperationException("The file was never written."));

    // Written beside the file first, so a failure keeps the old file.
    private static void Write(string target, byte[] bytes)
    {
        var temp = $"{target}.{Guid.NewGuid():N}.g3mtmp";
        try
        {
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, target, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}

/// <summary>Writes an edited picture over an image file in the Included Files folder (png, jpg, bmp or gif; the first frame of a gif).</summary>
public sealed class IncludedPictureCommand(string dataFile, string relative, byte[] picture, bool force = false) : IncludedBytesCommand(dataFile, relative, picture, force)
{
    public const string Name = "includedPicture";

    public override string Kind => Name;

    protected override string ArgName => "picture";

    protected override string RiskTitle => "risky.includedPictureTitle";

    protected override string RiskBody => "risky.includedPicture";

    public static IncludedPictureCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["dataFile"], a["path"], Convert.FromBase64String(a["picture"]), a.ContainsKey("force"));

    public static bool IsPicture(string path) => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif";

    protected override bool Accepts(string path) => IsPicture(path);

    protected override byte[] Encode(byte[] content, string target)
    {
        using var image = ReplaceFrameCommand.ReadPicture(content);
        image.Format = Path.GetExtension(target).ToLowerInvariant() switch { ".jpg" or ".jpeg" => MagickFormat.Jpeg, ".bmp" => MagickFormat.Bmp, ".gif" => MagickFormat.Gif, _ => MagickFormat.Png };
        return image.ToByteArray();
    }
}

/// <summary>Writes the text of an edited text file (txt, json, ini, csv, gml, yy, xml, md, cfg and the like) over a file in the Included Files folder, as UTF-8.</summary>
public sealed class IncludedTextCommand(string dataFile, string relative, string text, bool force = false) : IncludedBytesCommand(dataFile, relative, System.Text.Encoding.UTF8.GetBytes(text), force)
{
    public const string Name = "includedText";

    private static readonly HashSet<string> Extensions = [".txt", ".json", ".ini", ".csv", ".gml", ".yy", ".yyp", ".xml", ".md", ".cfg", ".yaml", ".yml", ".log", ".tsv"];

    public override string Kind => Name;

    protected override string ArgName => "text";

    protected override string RiskTitle => "risky.includedTextTitle";

    protected override string RiskBody => "risky.includedText";

    public static IncludedTextCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["dataFile"], a["path"], System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(a["text"])), a.ContainsKey("force"));

    public static bool IsText(string path) => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    protected override bool Accepts(string path) => IsText(path);

    protected override byte[] Encode(byte[] content, string target) => content;
}

/// <summary>Replaces the audio of an included sound file (ogg, wav, mp3) with the bytes of another audio file; the folder holds the bytes as they are.</summary>
public sealed class IncludedAudioCommand(string dataFile, string relative, byte[] audio, bool force = false) : IncludedBytesCommand(dataFile, relative, audio, force)
{
    public const string Name = "includedAudio";

    public override string Kind => Name;

    protected override string ArgName => "audio";

    protected override string RiskTitle => "risky.includedAudioTitle";

    protected override string RiskBody => "risky.includedAudio";

    public static IncludedAudioCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["dataFile"], a["path"], Convert.FromBase64String(a["audio"]), a.ContainsKey("force"));

    public static bool IsAudio(string path) => Path.GetExtension(path).ToLowerInvariant() is ".ogg" or ".wav" or ".mp3";

    protected override bool Accepts(string path) => IsAudio(path);

    protected override byte[] Encode(byte[] content, string target) => content.Length > 0 ? content : throw new InvalidOperationException("The audio file is empty.");
}

/// <summary>Replaces the audio of one embedded audio entry of the data file; the lengths of the sounds that play it follow the new audio. Undo puts back the old audio and lengths.</summary>
public sealed class ReplaceEmbeddedAudioCommand(string name, byte[] audio) : EditCommand
{
    public const string Name = "replaceEmbeddedAudio";

    private byte[]? _before;
    private List<(GameMakerSound Sound, float Length)> _lengths = [];

    public override string Kind => Name;

    public override string Editor => "embeddedAudio";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["name"] = name, ["audio"] = Convert.ToBase64String(audio) };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("EmbeddedAudio", null)];

    public static ReplaceEmbeddedAudioCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["name"], Convert.FromBase64String(a["audio"]));

    public override void Apply(GameMakerData data)
    {
        var entry = data.EmbeddedAudio.FirstOrDefault(e => e.Name.Content == name) ?? throw new InvalidOperationException($"Embedded audio '{name}' does not exist.");
        if (audio.Length == 0) throw new InvalidOperationException("The audio file is empty.");
        var seconds = SoundAudio.Seconds(audio);
        _before = entry.Data;
        _lengths = [.. data.Sounds.Where(s => ReferenceEquals(s.AudioFile, entry)).Select(s => (s, s.AudioLength))];
        entry.Data = audio;
        if (seconds is { } length)
            foreach (var (sound, _) in _lengths) sound.AudioLength = (float)length;
    }

    public override void Revert(GameMakerData data)
    {
        var entry = data.EmbeddedAudio.FirstOrDefault(e => e.Name.Content == name) ?? throw new InvalidOperationException($"Embedded audio '{name}' does not exist.");
        entry.Data = _before ?? throw new InvalidOperationException("The audio was never replaced.");
        foreach (var (sound, length) in _lengths) sound.AudioLength = length;
    }
}
