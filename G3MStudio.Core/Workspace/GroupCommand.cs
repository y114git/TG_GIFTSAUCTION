using System.Text.Json;

namespace G3MStudio.Core.Workspace;

/// <summary>Several edits as one undo step. They apply in order and revert in reverse order; the journal stores them as a JSON list.</summary>
public sealed class GroupCommand : EditCommand
{
    public const string Name = "group";

    public GroupCommand(IReadOnlyList<EditCommand> items)
    {
        Items = items;
        var touches = items.Select(i => i.Touches).ToList();
        Touches = touches.Contains(null) ? null : [.. touches.SelectMany(t => t ?? []).Distinct()];
    }

    public IReadOnlyList<EditCommand> Items { get; }

    public override string Kind => Name;

    public override string Editor => Items.Count > 0 ? Items[0].Editor : "group";

    public override IReadOnlyList<ProjectTouch>? Touches { get; }

    /// <summary>The items are read when asked, after they applied, because an item may fill in details (a new instance id) while applying.</summary>
    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["items"] = JsonSerializer.Serialize(Items.Select(i => new JournalRecord("do", i.Kind, i.Args.ToDictionary())).ToList()),
    };

    public static GroupCommand FromArgs(IReadOnlyDictionary<string, string> args) =>
        new([.. (JsonSerializer.Deserialize<List<JournalRecord>>(args["items"]) ?? []).Select(r => Create(r.Kind ?? "", r.Args ?? []))]);

    public override void Apply(G3MLib.DataFile.GameMakerData data)
    {
        var done = 0;
        try
        {
            foreach (var item in Items)
            {
                item.Apply(data);
                done++;
            }
        }
        catch
        {
            for (var i = done - 1; i >= 0; i--) Items[i].Revert(data);
            throw;
        }
    }

    public override void Revert(G3MLib.DataFile.GameMakerData data)
    {
        for (var i = Items.Count - 1; i >= 0; i--) Items[i].Revert(data);
    }
}
