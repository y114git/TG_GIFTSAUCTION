using System.Globalization;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>Changes the text of one entry of the string table. Every use of the entry shows the new text, so this is the "change all uses" edit.</summary>
public sealed class EditStringCommand : EditCommand
{
    public const string Name = "editString";

    private readonly int _index;
    private readonly string _text;
    private string? _old;

    public EditStringCommand(int index, string text) => (_index, _text) = (index, text);

    public override string Kind => Name;

    public override string Editor => "strings";

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["index"] = _index.ToString(CultureInfo.InvariantCulture),
        ["text"] = _text,
    };

    public override IReadOnlyList<ProjectTouch>? Touches => null;

    public static EditStringCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(int.Parse(a["index"], CultureInfo.InvariantCulture), a["text"]);

    private G3MLib.DataFile.Models.GameMakerString Entry(GameMakerData data) =>
        _index >= 0 && _index < data.Strings.Count ? data.Strings[_index] : throw new InvalidOperationException($"The string table has no entry {_index}.");

    public override void Apply(GameMakerData data)
    {
        var entry = Entry(data);
        _old = entry.Content;
        entry.Content = _text;
    }

    public override void Revert(GameMakerData data) => Entry(data).Content = _old;
}
