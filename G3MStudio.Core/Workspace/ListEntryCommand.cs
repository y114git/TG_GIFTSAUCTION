using System.Collections;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Adds a blank entry to the end of an editable list in a resource (tags, feature flags, constants, path-like point lists), removes the entry at an index, or swaps
/// it with its neighbour (UTMT-CTRL-06). Lists with their own commands are refused (<see cref="PropertyModel.IsEditable"/>).
/// Changing an entry in place is a <see cref="SetPropertyCommand"/> on the entry's path.
/// </summary>
public sealed class ListEntryCommand(string type, string name, string path, string operation, int index) : EditCommand
{
    public const string Name = "listEntry";
    public const string Add = "add";
    public const string Remove = "remove";
    public const string Up = "up";
    public const string Down = "down";

    private object? _entry;
    private int _at = -1;
    private IReadOnlyList<ProjectTouch>? _touches;

    public override string Kind => Name;

    public override string Editor => type;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["type"] = type, ["name"] = name, ["path"] = path, ["operation"] = operation, ["index"] = index.ToString(System.Globalization.CultureInfo.InvariantCulture) };


    public override IReadOnlyList<ProjectTouch>? Touches => _touches;

    public static ListEntryCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["type"], a["name"], a["path"], a["operation"], int.Parse(a["index"], System.Globalization.CultureInfo.InvariantCulture));

    private string ListName => path.Split('.')[^1].Split('[')[0];

    private IList ListOf(GameMakerData data, out object root)
    {
        root = PropertyModel.FindRoot(data, type, name) ?? throw new InvalidOperationException($"'{name}' was not found in {type}.");
        var list = PropertyModel.ReadRaw(root, path) as IList ?? throw new InvalidOperationException($"'{path}' is not a list.");
        if (!PropertyModel.IsEditable(list, ListName)) throw new InvalidOperationException($"'{path}' has its own editor.");
        return list;
    }

    private int Neighbour => operation == Up ? index - 1 : index + 1;

    public override void Apply(GameMakerData data)
    {
        var list = ListOf(data, out var root);
        switch (operation)
        {
            case Add:
                _at = list.Count;
                _entry ??= PropertyModel.NewEntry(data, list, ListName);
                list.Add(_entry);
                break;
            case Remove:
                if (index < 0 || index >= list.Count) throw new InvalidOperationException($"'{path}' has no entry {index}.");
                _at = index;
                _entry = list[index];
                list.RemoveAt(index);
                break;
            case Up or Down:
                if (index < 0 || index >= list.Count || Neighbour < 0 || Neighbour >= list.Count) throw new InvalidOperationException($"'{path}' entry {index} cannot move {operation}.");
                (list[index], list[Neighbour]) = (list[Neighbour], list[index]);
                break;
            default: throw new InvalidDataException($"Unknown list operation '{operation}'.");
        }

        var types = RenameResourceCommand.ProjectTypes(root);
        _touches = types is null ? null : [.. types.Select(t => new ProjectTouch(t, RenameResourceCommand.WholeFile.Contains(t) ? null : name)), new ProjectTouch("Strings", null)];
    }

    public override void Revert(GameMakerData data)
    {
        var list = ListOf(data, out _);
        if (operation == Add) list.RemoveAt(_at);
        else if (operation == Remove) list.Insert(Math.Clamp(_at, 0, list.Count), _entry);
        else (list[index], list[Neighbour]) = (list[Neighbour], list[index]);
    }
}
