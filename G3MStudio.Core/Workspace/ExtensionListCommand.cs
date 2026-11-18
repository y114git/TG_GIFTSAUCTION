using System.Globalization;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Adds an entry to a list of an extension (<c>Files</c>, <c>Options</c>, <c>Files[0].Functions</c>, <c>Files[0].Functions[1].Arguments</c>), removes one, or moves one to another place in
/// the same list (UTMT-AST-04 to 06). A new entry has empty names and the type defaults; its values are then edited as properties. Removing a function, or a file that has functions,
/// that code calls is refused with <see cref="CodeUseException"/> until <c>force</c> is set. Undo puts the very same entry back at its place.
/// </summary>
public sealed partial class ExtensionListCommand(string extension, string list, string operation, int index, int to = -1, bool force = false) : EditCommand
{
    public const string Name = "extensionList";
    public const string Add = "add";
    public const string Remove = "remove";
    public const string Move = "move";

    /// <summary>Limit of the GameMaker runner: a call into an extension function passes at most 17 arguments.</summary>
    public const int MaxArguments = 17;

    private const uint StdcallKind = 12;

    private object? _removed;
    private int _at = -1;
    private IReadOnlyList<ProjectTouch>? _touches;

    /// <summary>The kind (DLL, GML, JS, ...) of a file added by this command; empty or unknown means a DLL.</summary>
    public string FileKind { get; init; } = "";

    public override string Kind => Name;

    public override string Editor => "extensions";

    public override IReadOnlyDictionary<string, string> Args => BuildArgs();

    private Dictionary<string, string> BuildArgs()
    {
        var args = new Dictionary<string, string>
        {
            ["extension"] = extension,
            ["list"] = list,
            ["operation"] = operation,
            ["index"] = index.ToString(CultureInfo.InvariantCulture),
            ["to"] = to.ToString(CultureInfo.InvariantCulture),
        };
        if (force) args["force"] = "1";
        if (FileKind.Length > 0) args["fileKind"] = FileKind;
        return args;
    }

    public override IReadOnlyList<ProjectTouch>? Touches => _touches;

    public static ExtensionListCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["extension"], a["list"], a["operation"], int.Parse(a["index"], CultureInfo.InvariantCulture), int.Parse(a["to"], CultureInfo.InvariantCulture), a.ContainsKey("force")) { FileKind = a.GetValueOrDefault("fileKind", "") };

    public override void Apply(GameMakerData data)
    {
        var items = ListOf(data, out var root);
        switch (operation)
        {
            case Add:
                if (list.EndsWith("Arguments", StringComparison.Ordinal) && items.Count >= MaxArguments) throw new InvalidOperationException($"A function can have at most {MaxArguments} arguments.");
                _at = items.Count;
                items.Add(NewEntry(data, items, root, FileKind));
                break;
            case Remove:
                Check(items, index);
                if (!force && CalledNames(items[index]).FirstOrDefault(n => UsedInCode(data, n)) is { } used)
                    throw new CodeUseException($"'{used}' is called from code: change that code first, or allow risky actions in Settings > Advanced to remove it anyway.");
                _at = index;
                _removed = items[index];
                items.RemoveAt(index);
                break;
            case Move:
                Check(items, index);
                Check(items, to);
                (_removed, _at) = (items[index], index);
                items.RemoveAt(index);
                items.Insert(to, _removed);
                break;
            default:
                throw new InvalidDataException($"Unknown extension list operation '{operation}'.");
        }

        var types = RenameResourceCommand.ProjectTypes(root);
        _touches = types is null ? null : [.. types.Select(t => new ProjectTouch(t, RenameResourceCommand.WholeFile.Contains(t) ? null : extension)), new ProjectTouch("Strings", null)];
    }
}
