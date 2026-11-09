using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using G3MStudio.Core.ModPack;
using G3MStudio.Localization;

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
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var apply = new Button { Name = "PackApply", Content = Loc.Current["mods.pack.applyBtn"], IsEnabled = plan.Refusal is null && plan.Items.Count > 0 };
        var report = new Button { Name = "PackReport", Content = Loc.Current["mods.pack.saveReport"] };
        var cancel = new Button { Name = "PackReviewCancel", IsCancel = true, Content = Loc.Current["explorer.cancel"] };
        apply.Click += (_, _) => Close(_choices);
        cancel.Click += (_, _) => Close(null);
        report.Click += async (_, _) => await SaveReportAsync();
        var notes = new List<string> { Loc.Current.Format("mods.pack.summary", ("apply", plan.Count(PackState.Apply)), ("done", plan.Count(PackState.AlreadyApplied)), ("conflicts", plan.Count(PackState.Conflict)), ("missing", plan.Count(PackState.Missing))) };
        if (otherFile) notes.Add(Loc.Current["mods.pack.otherVersion"]);
        if (plan.Refusal is not null) notes.Add(plan.Refusal);
        var head = new TextBlock { Name = "PackSummary", Text = string.Join(Environment.NewLine, notes), TextWrapping = TextWrapping.Wrap, Margin = new Avalonia.Thickness(0, 0, 0, ModsUi.Space(2)) };
        var foot = ModsUi.Buttons(report, apply, cancel);
        foot.Margin = new Avalonia.Thickness(0, ModsUi.Space(2), 0, 0);
        DockPanel.SetDock(head, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(foot, Avalonia.Controls.Dock.Bottom);
        var panel = new DockPanel { Margin = ModsUi.Pad(4) };
        panel.Children.AddRange([head, foot, new ListBox { Name = "PackItems", ItemsSource = plan.Items.Select(Row).ToList() }]);
        Content = panel;
    }

    private StackPanel Row(PackItemPlan item)
    {
        var change = item.Entry.Change;
        var state = item.State switch
        {
            PackState.Apply => Loc.Current["mods.pack.state.apply"],
            PackState.AlreadyApplied => Loc.Current["mods.pack.state.alreadyApplied"],
            PackState.Conflict => Loc.Current["mods.pack.state.conflict"],
            _ => Loc.Current["mods.pack.state.missing"],
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = ModsUi.Space(2) };
        if (item.State == PackState.Conflict) row.Children.Add(Choice(item));
        row.Children.Add(new TextBlock { Text = $"{state}  {Loc.Current["explorer.type." + change.Type]}  {change.Name}", VerticalAlignment = VerticalAlignment.Center });
        if (item.Merge is { } merge) row.Children.Add(new TextBlock { Text = merge.Clean ? Loc.Current["mods.pack.mergeClean"] : Loc.Current.Format("mods.pack.mergeClash", ("count", merge.Conflicts)), Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private ComboBox Choice(PackItemPlan item)
    {
        var box = new ComboBox { Name = "Choice_" + item.Key, Width = 140 };
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
