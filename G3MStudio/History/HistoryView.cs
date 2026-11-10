using Avalonia.Controls;
using Avalonia.Controls.Templates;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.History;

/// <summary>One line of the history list: <see cref="Position"/> is the number of commands applied when this line is the current state.</summary>
public sealed record HistoryRow(int Position, string Text, bool Applied);

/// <summary>Edit > Undo History (design/shell.md 14.4 item 2): the whole stack newest first, a click undoes or redoes to that point.</summary>
public sealed class HistoryView : UserControl
{
    private readonly ListBox _list = new() { Name = "HistoryList" };
    private Workspace? _workspace;

    public HistoryView()
    {
        _list.ItemTemplate = new FuncDataTemplate<HistoryRow>((row, _) => new TextBlock
        {
            Text = row?.Text,
            Classes = { row?.Applied == false ? "muted" : "plain" },
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
        });
        _list.Tapped += (_, e) =>
        {
            if (e.Source is Control { DataContext: HistoryRow row }) GoTo(row);
        };
        Content = _list;
        // Loc is static: hold the handler only while the view is shown, so a closed window can be collected.
        void OnLanguage(object? _, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Loc.Current.Language)) Refresh();
        }

        AttachedToVisualTree += (_, _) => Loc.Current.PropertyChanged += OnLanguage;
        DetachedFromVisualTree += (_, _) => Loc.Current.PropertyChanged -= OnLanguage;
    }

    public IReadOnlyList<HistoryRow> Rows { get; private set; } = [];

    public void Bind(Workspace? workspace)
    {
        _workspace?.History.Changed -= Refresh;
        _workspace = workspace;
        workspace?.History.Changed += Refresh;
        Refresh();
    }

    public void GoTo(HistoryRow row)
    {
        if (_workspace is { History.Frozen: false } workspace) workspace.MoveTo(row.Position);
    }

    private void Refresh()
    {
        var rows = new List<HistoryRow>();
        if (_workspace is { } workspace)
        {
            var history = workspace.History;
            for (var i = history.Items.Count; i >= 1; i--)
                rows.Add(new HistoryRow(i, $"{history.Items[i - 1].Time:HH:mm:ss}  {Describe(history.Items[i - 1])}", i <= history.Position));
            rows.Add(new HistoryRow(0, Loc.Current["history.start"], true));
        }

        Rows = rows;
        _list.ItemsSource = rows;
        _list.SelectedItem = _workspace is null ? null : rows.FirstOrDefault(r => r.Position == _workspace.History.Position);
    }

    internal static string Describe(EditCommand command) => command switch
    {
        RenameResourceCommand => Loc.Current.Format("history.rename", ("old", command.Args["old"]), ("new", command.Args["new"])),
        ResourceListCommand when command.Args["operation"] == ResourceListCommand.New => Loc.Current.Format("history.newResource", ("type", TypeLabel(command.Args["type"])), ("name", command.Args["name"])),
        ResourceListCommand when command.Args["operation"] == ResourceListCommand.Delete => Loc.Current.Format("history.deleteResource", ("type", TypeLabel(command.Args["type"])), ("name", command.Args["name"])),
        EditCodeCommand => Loc.Current.Format("history.code", ("entry", command.Args["entry"])),
        CopyResourcesCommand copy => copy.Steps.Count == 1
            ? Loc.Current.Format("history.copyResource", ("type", TypeLabel(copy.Steps[0].Type)), ("name", copy.Steps[0].Name))
            : Loc.Current.Format("history.copyResources", ("count", copy.Steps.Count)),
        GroupCommand group => DescribeGroup(group),
        _ => command.Kind,
    };

    // A group made around resource lists (a delete that clears uses first, a copy with its code) is named by the list edits inside it.
    private static string DescribeGroup(GroupCommand group)
    {
        var lists = Flatten(group).OfType<ResourceListCommand>().ToList();
        if (lists.Count == 0) return group.Kind;
        var operation = lists[0].Args["operation"];
        if (lists.Count > 1 && lists.All(l => l.Args["operation"] == operation))
            return Loc.Current.Format(operation == ResourceListCommand.Delete ? "history.deleteResources" : "history.newResources", ("count", lists.Count));
        // A mix is a new resource with edits around it, or a delete: the first new one names it, else the last list edit.
        return Describe(lists.FirstOrDefault(l => l.Args["operation"] == ResourceListCommand.New) ?? lists[^1]);
    }

    private static IEnumerable<EditCommand> Flatten(GroupCommand group) => group.Items.SelectMany(i => i is GroupCommand inner ? Flatten(inner) : [i]);

    // Types that cannot be created have no singular name of their own, so they use the name of their list.
    private static string TypeLabel(string type) => Loc.Current["explorer.typeOne." + type] is var one && one != "explorer.typeOne." + type ? one : Loc.Current["explorer.type." + type];
}
