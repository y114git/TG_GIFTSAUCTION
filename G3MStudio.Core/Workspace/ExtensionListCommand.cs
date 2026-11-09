using System.Collections;
using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Adds an entry to a list of an extension (<c>Files</c>, <c>Options</c>, <c>Files[0].Functions</c>, <c>Files[0].Functions[1].Arguments</c>), removes one, or moves one to another place in
/// the same list (UTMT-AST-04 to 06). A new entry has empty names and the type defaults; its values are then edited as properties. Removing a function, or a file that has functions,
/// that code calls is refused with <see cref="CodeUseException"/> until <c>force</c> is set. Undo puts the very same entry back at its place.
/// </summary>
public sealed class ExtensionListCommand(string extension, string list, string operation, int index, int to = -1, bool force = false) : EditCommand
{
    public const string Name = "extensionList";
    public const string Add = "add";
    public const string Remove = "remove";
    public const string Move = "move";

    /// <summary>The runner takes at most this many arguments for an extension function.</summary>
    public const int MaxArguments = 17;

    private const uint StdcallKind = 12;

    private object? _removed;
    private int _at = -1;
    private IReadOnlyList<ProjectTouch>? _touches;

    public override string Kind => Name;

    public override string Editor => "extensions";

    public override IReadOnlyDictionary<string, string> Args { get; } = BuildArgs(extension, list, operation, index, to, force);

    private static Dictionary<string, string> BuildArgs(string extension, string list, string operation, int index, int to, bool force)
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
        return args;
    }

    public override IReadOnlyList<ProjectTouch>? Touches => _touches;

    public static ExtensionListCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["extension"], a["list"], a["operation"], int.Parse(a["index"], CultureInfo.InvariantCulture), int.Parse(a["to"], CultureInfo.InvariantCulture), a.ContainsKey("force"));

    private IList ListOf(GameMakerData data, out object root)
    {
        root = PropertyModel.FindRoot(data, "extensions", extension) ?? throw new InvalidOperationException($"Extension '{extension}' does not exist.");
        return PropertyModel.ReadRaw(root, list) as IList ?? throw new InvalidOperationException($"'{list}' is not a list.");
    }

    public override void Apply(GameMakerData data)
    {
        var items = ListOf(data, out var root);
        switch (operation)
        {
            case Add:
                if (list.EndsWith("Arguments", StringComparison.Ordinal) && items.Count >= MaxArguments) throw new InvalidOperationException($"A function can have at most {MaxArguments} arguments.");
                _at = items.Count;
                items.Add(NewEntry(data, items, root));
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

    public override void Revert(GameMakerData data)
    {
        var items = ListOf(data, out _);
        switch (operation)
        {
            case Add:
                items.RemoveAt(_at);
                break;
            case Remove:
                items.Insert(Math.Clamp(_at, 0, items.Count), _removed);
                break;
            case Move:
                items.RemoveAt(to);
                items.Insert(_at, _removed);
                break;
        }
    }

    private void Check(IList items, int at)
    {
        if (at < 0 || at >= items.Count) throw new InvalidOperationException($"'{list}' has no entry {at}.");
    }

    /// <summary>The GML names a removed entry takes out: the function itself, or every function of a file.</summary>
    private static IEnumerable<string> CalledNames(object? entry) => entry switch
    {
        GameMakerExtensionFunction f => f.Name?.Content is { Length: > 0 } n ? [n] : [],
        GameMakerExtensionFile file => file.Functions.SelectMany(CalledNames),
        _ => [],
    };

    private static bool UsedInCode(GameMakerData data, string name)
    {
        var function = data.Functions?.FirstOrDefault(f => f.Name?.Content == name);
        return function is not null && ReferenceCache.Of(data).Get(data).FindUsages(function).Any(u => u.InstructionOffset >= 0);
    }

    // A new entry: empty strings from the table and the first valid value of every type field, so the entry can be written to the file as it is.
    private static object NewEntry(GameMakerData data, IList items, object root)
    {
        var type = items.GetType().GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>)).GetGenericArguments()[0];
        var entry = Activator.CreateInstance(type)!;
        foreach (var property in type.GetProperties().Where(p => p.PropertyType == typeof(GameMakerString) && p.CanWrite)) property.SetValue(entry, data.Strings.MakeString("", out _));
        switch (entry)
        {
            case GameMakerExtensionFile file:
                file.Kind = GameMakerExtensionKind.Dll;
                break;
            case GameMakerExtensionFunction function:
                function.RetType = GameMakerExtensionVarType.Double;
                function.Kind = items.OfType<GameMakerExtensionFunction>().LastOrDefault()?.Kind ?? StdcallKind;
                function.ID = ((GameMakerExtension)root).Files.SelectMany(f => f.Functions).Select(f => f.ID).DefaultIfEmpty().Max() + 1;
                break;
        }

        return entry;
    }
}
