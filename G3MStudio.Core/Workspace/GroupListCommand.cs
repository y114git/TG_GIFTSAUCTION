using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Adds a texture group or an audio group at the end of its list, or renames one. A rename gives the group a string of its own, so no other use of the old text changes.
/// Undo takes the group out again or puts the old name back.
/// </summary>
public sealed partial class GroupListCommand(bool audio, string operation, string name, string newName = "") : EditCommand
{
    public const string Name = "groupList";
    public const string New = "new";
    public const string Rename = "rename";

    private object? _added;
    private GameMakerString? _oldName;

    public override string Kind => Name;

    public override string Editor => audio ? "audioGroups" : "textureGroups";

    public override bool ChangesNames => false;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["audio"] = audio ? "1" : "0",
        ["operation"] = operation,
        ["name"] = name,
        ["newName"] = newName,
    };

    public override IReadOnlyList<ProjectTouch>? Touches => null;

    public static GroupListCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["audio"] == "1", a["operation"], a["name"], a["newName"]);

    private static IEnumerable<string> NamesOf(GameMakerData data, bool audio) =>
        audio ? (data.AudioGroups ?? []).Select(g => g.Name.Content ?? "") : (data.TextureGroupInfo ?? []).Select(g => g.Name.Content ?? "");

    private void CheckFree(GameMakerData data, string text)
    {
        if (!ResourceListCommand.IsValidName(text)) throw new InvalidOperationException($"'{text}' is not a plain name: letters, digits and underscores, not starting with a digit.");
        if (NamesOf(data, audio).Contains(text)) throw new InvalidOperationException($"A group named '{text}' exists already.");
    }

    public override void Apply(GameMakerData data)
    {
        if (operation == New) Add(data);
        else if (operation == Rename) Retitle(data);
        else throw new InvalidDataException($"Unknown group operation '{operation}'.");
    }
}
