using System.Text.Json;

namespace G3MStudio.Core.Workspace;

/// <summary>Several edits as one undo step. They apply in order and revert in reverse order; the journal stores them as a JSON list.</summary>
public sealed class GroupCommand : EditCommand
{
    public const string Name = "group";

    private readonly IReadOnlyList<EditCommand> _items;

    public GroupCommand(IReadOnlyList<EditCommand> items)
    {
        _items = items;
        var touches = items.Select(i => i.Touches).ToList();
        Touches = touches.Contains(null) ? null : [.. touches.SelectMany(t => t ?? []).Distinct()];
    }

    public override string Kind => Name;

    public override string Editor => _items.Count > 0 ? _items[0].Editor : "group";

    public override IReadOnlyList<ProjectTouch>? Touches { get; }

    /// <summary>The items are read when asked, after they applied, because an item may fill in details (a new instance id) while applying.</summary>
    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["items"] = JsonSerializer.Serialize(_items.Select(i => new JournalRecord("do", i.Kind, i.Args.ToDictionary())).ToList()),
    };

    public static GroupCommand FromArgs(IReadOnlyDictionary<string, string> args) =>
        new([.. (JsonSerializer.Deserialize<List<JournalRecord>>(args["items"]) ?? []).Select(r => Create(r.Kind ?? "", r.Args ?? []))]);

    public override void Apply(G3MLib.DataFile.GameMakerData data)
    {
        var done = 0;
        try
        {
            foreach (var item in _items)
            {
                item.Apply(data);
                done++;
            }
        }
        catch
        {
            for (var i = done - 1; i >= 0; i--) _items[i].Revert(data);
            throw;
        }
    }

    public override void Revert(G3MLib.DataFile.GameMakerData data)
    {
        for (var i = _items.Count - 1; i >= 0; i--) _items[i].Revert(data);
    }
}
