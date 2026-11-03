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
        Loc.Current.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Loc.Current.Language)) Refresh(); };
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

    private static string Describe(EditCommand command) => command switch
    {
        RenameResourceCommand => Loc.Current.Format("history.rename", ("old", command.Args["old"]), ("new", command.Args["new"])),
        EditCodeCommand => Loc.Current.Format("history.code", ("entry", command.Args["entry"])),
        _ => command.Kind,
    };
}
