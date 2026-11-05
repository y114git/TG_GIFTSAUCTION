using System.Text;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>Makes a new resource from a plain file: a picture becomes a sprite, a WAV, OGG or MP3 file a sound, a GML text a script. Used by drop onto the window and by File > Import Files.</summary>
public static class FileImport
{
    private static readonly string[] Pictures = [".png", ".bmp", ".gif", ".jpg", ".jpeg", ".webp"];

    /// <summary>The resource type the file makes ("sprites", "sounds", "scripts"), or null when the extension is not one of them.</summary>
    public static string? TypeOf(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return Pictures.Contains(extension) ? "sprites" : extension is ".wav" or ".ogg" or ".mp3" ? "sounds" : extension == ".gml" ? "scripts" : null;
    }

    /// <summary>A free resource name from the file name: other characters become underscores, a leading digit gets one, a taken name gets a number.</summary>
    public static string NameFor(GameMakerData data, string path)
    {
        var stem = new string([.. Path.GetFileNameWithoutExtension(path).Select(c => char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_')]);
        if (stem.Length == 0 || char.IsAsciiDigit(stem[0])) stem = "_" + stem;
        var name = stem;
        for (var n = 1; ResourceListCommand.NameTaken(data, name) || data.Code.ByName("gml_Script_" + name) is not null; n++) name = $"{stem}_{n}";
        return name;
    }

    /// <summary>The one undoable edit that adds the resource and fills it from the file. The picture or audio is checked before anything reaches the history.</summary>
    public static EditCommand Build(GameMakerData data, string path, out string type, out string name, bool asText = false)
    {
        type = TypeOf(path) ?? (asText ? "scripts" : throw new RiskyRefusalException("risky.importTypeTitle", "risky.importType", $"'{Path.GetFileName(path)}' is not a picture, sound or GML file."));
        name = NameFor(data, path);
        var (kind, own) = (type, name); // out parameters cannot be used inside the lambda below
        var make = ResourceTemplates.New(data, kind, own);
        switch (kind)
        {
            case "sprites":
                var picture = File.ReadAllBytes(path);
                using (var image = ReplaceFrameCommand.ReadPicture(picture))
                {
                    var (w, h) = ((int)image.Width, (int)image.Height);
                    return new GroupCommand([make, new FrameOwnPictureCommand(own, 0, picture), .. new (string, int)[] { ("Width", w), ("Height", h), ("MarginRight", w - 1), ("MarginBottom", h - 1) }
                        .Select(p => new SetPropertyCommand("sprites", own, p.Item1, p.Item2.ToString(System.Globalization.CultureInfo.InvariantCulture)))]);
                }

            case "sounds":
                var audio = File.ReadAllBytes(path);
                var format = SoundAudio.Format(audio);
                if (format is not ("wav" or "ogg" or "mp3")) throw new InvalidDataException($"'{Path.GetFileName(path)}' is not a WAV, OGG or MP3 file.");
                // A data file stores WAV or OGG; MP3 becomes OGG, as GameMaker does when it compiles.
                if (format == "mp3") audio = AudioCodec.Convert(audio, "ogg");
                return new GroupCommand([make, new ReplaceAudioCommand(own, audio, changeKind: true)]);
            default:
                var text = File.ReadAllText(path, Encoding.UTF8);
                if (text.Contains((char)0, StringComparison.Ordinal)) throw new InvalidDataException($"'{Path.GetFileName(path)}' is a binary file, not text; a script cannot hold it.");
                return new GroupCommand([make, new EditCodeCommand("gml_Script_" + own, text)]);
        }
    }
}
