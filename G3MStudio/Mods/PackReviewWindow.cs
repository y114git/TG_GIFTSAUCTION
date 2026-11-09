using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using G3MStudio.Core.ModPack;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Mods;

/// <summary>The dry run of a pack on the open file: what each change would do, and for each conflict the choice Keep mine, Keep theirs or Merge code. Nothing is changed until Apply.</summary>
public sealed class PackReviewWindow : Window
{
    private readonly ReapplyPlan _plan;
    private readonly Dictionary<string, Resolution> _choices = [];

    public PackReviewWindow(ReapplyPlan plan, bool otherFile)
    {
        _plan = plan;
        Title = Loc.Current.Format("mods.pack.reapplyTitle", ("name", plan.Pack.Manifest.Name), ("version", plan.Pack.Manifest.Version));
        Width = 760;
        Height = 560;
        MinWidth = 480;
        MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var apply = DialogFrame.Action(this, Loc.Current["mods.pack.applyBtn"], "PackApply", () => Close(_choices), DialogRole.Primary);
        apply.IsEnabled = plan.Refusal is null && plan.Items.Count > 0;
        var report = new Button { Name = "PackReport", Content = Loc.Current["mods.pack.saveReport"] };
        report.Click += async (_, _) => await SaveReportAsync();
        var notes = new List<string> { Loc.Current.Format("mods.pack.summary", ("apply", plan.Count(PackState.Apply)), ("done", plan.Count(PackState.AlreadyApplied)), ("conflicts", plan.Count(PackState.Conflict)), ("missing", plan.Count(PackState.Missing))) };
        if (otherFile) notes.Add(Loc.Current["mods.pack.otherVersion"]);
        if (plan.Refusal is not null) notes.Add(plan.Refusal);
        var head = new TextBlock { Name = "PackSummary", Text = string.Join(Environment.NewLine, notes), TextWrapping = TextWrapping.Wrap, Margin = new Avalonia.Thickness(0, 0, 0, ModsUi.Space(2)) };
        DockPanel.SetDock(head, Avalonia.Controls.Dock.Top);
        var panel = new DockPanel();
        panel.Children.AddRange([head, new ListBox { Name = "PackItems", ItemsSource = plan.Items.Select(Row).ToList() }]);
        DialogFrame.Show(this, panel, [report, apply, DialogFrame.Cancel(this, () => Close(null), "PackReviewCancel")]);
    }

    private Grid Row(PackItemPlan item)
    {
        var change = item.Entry.Change;
        var state = item.State switch
        {
            PackState.Apply => Loc.Current["mods.pack.state.apply"],
            PackState.AlreadyApplied => Loc.Current["mods.pack.state.alreadyApplied"],
            PackState.Conflict => Loc.Current["mods.pack.state.conflict"],
            _ => Loc.Current["mods.pack.state.missing"],
        };
        var row = new Grid { ColumnSpacing = ModsUi.Space(2) };
        void Put(Control control)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition(row.ColumnDefinitions.Count == (item.State == PackState.Conflict ? 1 : 0) ? new GridLength(1, GridUnitType.Star) : GridLength.Auto));
            Grid.SetColumn(control, row.ColumnDefinitions.Count - 1);
            row.Children.Add(control);
        }

        if (item.State == PackState.Conflict) Put(Choice(item));
        Put(new TextBlock { Text = $"{state}  {Loc.Current["explorer.type." + change.Type]}  {change.Name}", VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        if (item.Merge is { } merge) Put(new TextBlock { Text = merge.Clean ? Loc.Current["mods.pack.mergeClean"] : Loc.Current.Format("mods.pack.mergeClash", ("count", merge.Conflicts)), Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        return row;
    }

    private ComboBox Choice(PackItemPlan item)
    {
        var box = new ComboBox { Name = "Choice_" + item.Key, MinWidth = 140 };
        var options = new List<Resolution> { Resolution.KeepMine, Resolution.KeepTheirs };
        if (item.Merge is { Clean: true }) options.Add(Resolution.MergeCode);
        foreach (var option in options) box.Items.Add(option switch { Resolution.KeepMine => Loc.Current["mods.pack.keepMine"], Resolution.KeepTheirs => Loc.Current["mods.pack.keepTheirs"], _ => Loc.Current["mods.pack.merge"] });
        box.SelectedIndex = 0;
        box.SelectionChanged += (_, _) => _choices[item.Key] = options[Math.Max(box.SelectedIndex, 0)];
        return box;
    }

    private async Task SaveReportAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = Loc.Current["mods.pack.saveReport"], DefaultExtension = "txt" });
        if (file?.TryGetLocalPath() is { } path) await File.WriteAllTextAsync(path, _plan.Report(_choices));
    }

    /// <summary>The choices for the conflicts (a conflict not listed keeps the file as it is), or null when cancelled.</summary>
    public async Task<IReadOnlyDictionary<string, Resolution>?> AskAsync(Window owner) => await ShowDialog<IReadOnlyDictionary<string, Resolution>?>(owner);
}
