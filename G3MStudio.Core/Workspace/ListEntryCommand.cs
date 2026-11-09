using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Adds an empty string to the end of a list of strings in a resource (tags, feature flags, sequence messages), or removes the entry at an index (UTMT-CTRL-06).
/// Changing an entry in place is a <see cref="SetPropertyCommand"/> on the entry's path.
/// </summary>
public sealed class ListEntryCommand(string type, string name, string path, string operation, int index) : EditCommand
{
    public const string Name = "listEntry";
    public const string Add = "add";
    public const string Remove = "remove";

    private GameMakerString? _removed;
    private int _at = -1;
    private IReadOnlyList<ProjectTouch>? _touches;

    public override string Kind => Name;

    public override string Editor => type;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["type"] = type, ["name"] = name, ["path"] = path, ["operation"] = operation, ["index"] = index.ToString(System.Globalization.CultureInfo.InvariantCulture) };


    public override IReadOnlyList<ProjectTouch>? Touches => _touches;

    public static ListEntryCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["type"], a["name"], a["path"], a["operation"], int.Parse(a["index"], System.Globalization.CultureInfo.InvariantCulture));

    private IList<GameMakerString> ListOf(GameMakerData data, out object root)
    {
        root = PropertyModel.FindRoot(data, type, name) ?? throw new InvalidOperationException($"'{name}' was not found in {type}.");
        return PropertyModel.ReadRaw(root, path) as IList<GameMakerString> ?? throw new InvalidOperationException($"'{path}' is not a list of strings.");
    }

    public override void Apply(GameMakerData data)
    {
        var list = ListOf(data, out var root);
        if (operation == Add)
        {
            _at = list.Count;
            list.Add(data.Strings.MakeString("", out _));
        }
        else if (operation == Remove)
        {
            if (index < 0 || index >= list.Count) throw new InvalidOperationException($"'{path}' has no entry {index}.");
            _at = index;
            _removed = list[index];
            list.RemoveAt(index);
        }
        else throw new InvalidDataException($"Unknown list operation '{operation}'.");

        var types = RenameResourceCommand.ProjectTypes(root);
        _touches = types is null ? null : [.. types.Select(t => new ProjectTouch(t, RenameResourceCommand.WholeFile.Contains(t) ? null : name)), new ProjectTouch("Strings", null)];
    }

    public override void Revert(GameMakerData data)
    {
        var list = ListOf(data, out _);
        if (operation == Add) list.RemoveAt(_at);
        else if (_removed is not null) list.Insert(Math.Clamp(_at, 0, list.Count), _removed);
    }
}
