using G3MStudio.Localization;

namespace G3MStudio.Editor;

public sealed partial class PropertyGridTab
{
    /// <summary>The field name as words: MarginLeft shows as "Margin Left", and an S field such as SVersion as "Special Version" (<c>properties.special</c>). The path, filter and edits keep the field name.</summary>
    public static string Spaced(string name) => name is ['S', >= 'A' and <= 'Z', >= 'a' and <= 'z', ..] ? Loc.Current.Format("properties.special", ("name", Spaced(name[1..]))) : string.Join(' ', SplitWords(name).Select(w => WordFixes.GetValueOrDefault(w, w)));

    private static string[] SplitWords(string name) => string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])) ? " " + c : c.ToString())).Split(' ');

    // Short forms of the data model that read badly as words, and abbreviations that hold several words (always upper case).
    private static readonly Dictionary<string, string> WordFixes = new() { ["Id"] = "ID", ["Ids"] = "IDs", ["Obj"] = "Object", ["Gml"] = "GML", ["Ui"] = "UI", ["Dll"] = "DLL", ["Js"] = "JS", ["Png"] = "PNG", ["Json"] = "JSON", ["Url"] = "URL", ["Xml"] = "XML", ["Glsl"] = "GLSL", ["Hlsl"] = "HLSL", ["Wav"] = "WAV", ["Ogg"] = "OGG", ["Gif"] = "GIF", ["Jpg"] = "JPG", ["Jpeg"] = "JPEG", ["Bmp"] = "BMP", ["Csv"] = "CSV", ["Gpu"] = "GPU", ["Cpu"] = "CPU", ["Sdk"] = "SDK", ["Api"] = "API", ["Ini"] = "INI", ["Html"] = "HTML", ["Css"] = "CSS", ["Tga"] = "TGA", ["Pcm"] = "PCM", ["Rgb"] = "RGB", ["Rgba"] = "RGBA", ["Fps"] = "FPS", ["Xaml"] = "XAML", ["Mp3"] = "MP3", ["Gui"] = "GUI", ["Cli"] = "CLI", ["Ide"] = "IDE", ["Yy"] = "YY", ["Yyp"] = "YYP", ["Dds"] = "DDS", ["Exe"] = "EXE", ["Bz2"] = "BZ2", ["Qoi"] = "QOI", ["Guid"] = "GUID", ["Uid"] = "UID", ["Bgra"] = "BGRA", ["Tpag"] = "TPAG", ["Xor"] = "XOR", ["Rle"] = "RLE", ["Sdf"] = "SDF", ["Swf"] = "SWF", ["Pssl"] = "PSSL", ["Iap"] = "IAP", ["Mdi"] = "MDI", ["Cjk"] = "CJK", ["Os"] = "OS", ["Pc"] = "PC", ["Vm"] = "VM", ["Ip"] = "IP", ["Yyc"] = "YYC", ["Txt"] = "TXT", ["Pdf"] = "PDF", ["Ttf"] = "TTF", ["Mp4"] = "MP4" };

    /// <summary>The label of a row: the language's own text (<c>properties.label.&lt;field&gt;</c>), else the field name as words.</summary>
    public static string Label(string name) => Translated("label", name) ?? Spaced(name);

    /// <summary>One flag of a flags field, as a check box label.</summary>
    public static string FlagLabel(string name) => Translated("choice", name) ?? Spaced(name);

    // The version of the nicknames the rows were built with.
    private int _nicknameVersion;

    private static string? Translated(string group, string name) => Loc.Current.Has($"properties.{group}.{name}") ? Loc.Current[$"properties.{group}.{name}"] : null;

    /// <summary>A choice of an enum as a person reads it (<c>properties.choice.&lt;name&gt;</c>): "FramesPerSecond" shows as "Frames per second" when there is no text. The stored value keeps its name.</summary>
    public static string ChoiceLabel(string name)
    {
        if (Translated("choice", name) is { } text) return text;
        var words = Spaced(name).Split(' ');
        return string.Join(' ', words.Select((w, i) => i == 0 || w.All(char.IsUpper) ? w : w.ToLowerInvariant()));
    }
}
