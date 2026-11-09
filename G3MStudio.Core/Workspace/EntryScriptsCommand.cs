using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Adds a code entry to the scripts the game runs at its start (<see cref="GlobalInit"/>) or at its end (<see cref="GameEnd"/>), or removes one from them (UTMT-AST-13, UTMT-AST-14).
/// Entries are found by the name of their code, so the command stays right when other edits move the list.
/// </summary>
#pragma warning disable CA2000 // a new list entry belongs to the data file's list, which disposes it
public sealed class EntryScriptsCommand(string list, string operation, string code) : EditCommand
{
    public const string Name = "entryScripts";
    public const string GlobalInit = "globalInit";
    public const string GameEnd = "gameEnd";
    public const string Add = "add";
    public const string Remove = "remove";

    private int _at = -1;
    private GameMakerGlobalInit? _removed;

    public override string Kind => Name;

    public override string Editor => "general";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["list"] = list, ["operation"] = operation, ["code"] = code };

    public static EntryScriptsCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["list"], a["operation"], a["code"]);

    public static IList<GameMakerGlobalInit> ListOf(GameMakerData data, string list) => (list switch
    {
        GlobalInit => data.GlobalInitScripts,
        GameEnd => data.GameEndScripts,
        _ => throw new InvalidDataException($"Unknown script list '{list}'."),
    }) ?? throw new InvalidOperationException("This data file has no such script list.");

    public override void Apply(GameMakerData data)
    {
        var items = ListOf(data, list);
        var found = items.FirstOrDefault(i => i.Code?.Name?.Content == code);
        if (operation == Add)
        {
            var entry = data.Code.FirstOrDefault(c => c.Name.Content == code) ?? throw new InvalidOperationException($"Code '{code}' does not exist.");
            if (found is not null) throw new InvalidOperationException($"'{code}' is already in the list.");
            _at = items.Count;
            items.Add(Make(entry)); // the list owns the entry from here on
        }
        else if (operation == Remove)
        {
            if (found is null) throw new InvalidOperationException($"'{code}' is not in the list.");
            _at = items.IndexOf(found);
            _removed = found;
            items.Remove(found);
        }
        else throw new InvalidDataException($"Unknown script list operation '{operation}'.");
    }

    public override void Revert(GameMakerData data)
    {
        var items = ListOf(data, list);
        if (operation == Add)
        {
            if (items.FirstOrDefault(i => i.Code?.Name?.Content == code) is { } added) items.Remove(added);
        }
        else
        {
            items.Insert(Math.Clamp(_at, 0, items.Count), _removed ?? Make(data.Code.FirstOrDefault(c => c.Name.Content == code) ?? throw new InvalidOperationException($"Code '{code}' does not exist.")));
        }
    }

    private static GameMakerGlobalInit Make(GameMakerCode code) => new() { Code = code };

    public static string Describe(GameMakerGlobalInit item) => item.Code?.Name?.Content ?? string.Empty;
}
#pragma warning restore CA2000
