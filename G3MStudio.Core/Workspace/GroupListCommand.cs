using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Adds a texture group or an audio group at the end of its list, or renames one. A rename gives the group a string of its own, so no other use of the old text changes.
/// Undo takes the group out again or puts the old name back. <c>force</c>: the owner chose a name that is not a plain GML name (a duplicate stays refused, as two groups of one name break the lookup by name).
/// </summary>
public sealed partial class GroupListCommand(bool audio, string operation, string name, string newName = "", bool force = false) : EditCommand
{
    public const string Name = "groupList";
    public const string New = "new";
    public const string Rename = "rename";

    private object? _added;
    private GameMakerString? _oldName;

    public override string Kind => Name;

    public override string Editor => audio ? "audioGroups" : "textureGroups";

    public override bool ChangesNames => false;

    public override IReadOnlyDictionary<string, string> Args { get; } = ArgsOf(audio, operation, name, newName, force);

    private static Dictionary<string, string> ArgsOf(bool audio, string operation, string name, string newName, bool force)
    {
        var args = new Dictionary<string, string> { ["audio"] = audio ? "1" : "0", ["operation"] = operation, ["name"] = name, ["newName"] = newName };
        if (force) args["force"] = "1";
        return args;
    }

    public override IReadOnlyList<ProjectTouch>? Touches => null;

    public static GroupListCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["audio"] == "1", a["operation"], a["name"], a["newName"], a.ContainsKey("force"));

    private static IEnumerable<string> NamesOf(GameMakerData data, bool audio) =>
        audio ? (data.AudioGroups ?? []).Select(g => g.Name.Content ?? "") : (data.TextureGroupInfo ?? []).Select(g => g.Name.Content ?? "");

    private void CheckFree(GameMakerData data, string text)
    {
        if (!CopyPackage.IsSafeName(text)) throw new InvalidOperationException($"'{text}' cannot be a name: it is empty, too long, or has characters that a file name cannot hold.");
        if (NamesOf(data, audio).Contains(text)) throw new InvalidOperationException($"A group named '{text}' exists already.");
        if (!force && !ResourceListCommand.IsValidName(text)) throw new UnusualNameException($"'{text}' is not a plain name: letters, digits and underscores, not starting with a digit. Code cannot use other names. Allow risky actions in Settings > Advanced to use it anyway.");
    }

    public override void Apply(GameMakerData data)
    {
        if (operation == New) Add(data);
        else if (operation == Rename) Retitle(data);
        else throw new InvalidDataException($"Unknown group operation '{operation}'.");
    }
}
