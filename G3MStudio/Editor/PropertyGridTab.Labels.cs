using Avalonia.Input;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

public sealed partial class PropertyGridTab
{
    /// <summary>The field name as words: MarginLeft shows as "Margin Left", and the sprite fields SVersion and SSpriteType as "Special Version" and "Special Sprite Type". The path, filter and edits keep the field name.</summary>
    public static string Spaced(string name) => name is ['S', >= 'A' and <= 'Z', >= 'a' and <= 'z', ..] ? "Special " + Spaced(name[1..]) : string.Join(' ', SplitWords(name).Select(w => WordFixes.GetValueOrDefault(w, w)));

    private static string[] SplitWords(string name) => string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])) ? " " + c : c.ToString())).Split(' ');

    // Short forms of the data model that read badly as words.
    private static readonly Dictionary<string, string> WordFixes = new() { ["Id"] = "ID", ["Ids"] = "IDs", ["Obj"] = "Object", ["Gml"] = "GML", ["Ui"] = "UI" };

    /// <summary>Fields whose label is a language key (<c>properties.label.&lt;field&gt;</c>), because the words of the field name alone do not make a good label.</summary>
    public static readonly string[] LabelFields = ["ParentId", "TextureMaskId", "PhysicsVertices", "LastObj", "LastTile", "IsDebuggerDisabled", "AudioID"];

    /// <summary>The label of a row: the language's own text for a known field, else the field name as words.</summary>
    public static string Label(string name) => LabelFields.Contains(name) ? Loc.Current["properties.label." + name] : Spaced(name);

    /// <summary>A choice of an enum as a person reads it: "FramesPerSecond" shows as "Frames per second". The stored value keeps its name.</summary>
    public static string ChoiceLabel(string name)
    {
        if (EnumFixes.TryGetValue(name, out var fixedName)) return fixedName;
        var words = Spaced(name).Split(' ');
        return string.Join(' ', words.Select((w, i) => i == 0 || w.All(char.IsUpper) ? w : w.ToLowerInvariant()));
    }

    private static readonly Dictionary<string, string> EnumFixes = new() { ["Oneshot"] = "One shot" };
}
