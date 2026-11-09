using G3MStudio.Theme;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>
/// The preview of Copy to Other Data File: every item that would be copied with what it uses, and for each name that exists in the target the choice
/// to rename, skip or replace. The list is read again whenever a choice changes; the answer is the plan to apply, or null when cancelled.
/// </summary>
public sealed class CopyToWindow : Window
{
    private readonly Workspace _source, _target;
    private readonly IReadOnlyList<(string Type, string Name)> _items;
    private readonly CheckBox _dependencies = new() { Name = "CopyDependencies", Content = Loc.Current["copyTo.deps"], IsChecked = true };
    private readonly ComboBox _mode = new() { Name = "CopyDefaultMode", SelectedIndex = 0 };
    private readonly ListBox _list = new() { Name = "CopyPlanList", MaxHeight = 320, MinHeight = 120 };
    private readonly TextBlock _notes = new TextBlock { Name = "CopyNotes", TextWrapping = TextWrapping.Wrap }.TextSize("FontSize.Small");
    private readonly Button _ok = new() { Name = "CopyOk", Content = Loc.Current["copyTo.ok"], IsEnabled = false };
    private readonly Dictionary<string, CollisionMode> _overrides = [];
    private int _version;
    private bool _rendering;

    public CopyToWindow(Workspace source, Workspace target, IReadOnlyList<(string Type, string Name)> items)
    {
        (_source, _target, _items) = (source, target, items);
        Title = Loc.Current["copyTo.title"];
        Width = 640;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _notes.Foreground = (IBrush?)Avalonia.Application.Current?.FindResource("Text.Muted");
        foreach (var key in new[] { "copyTo.modeRename", "copyTo.modeSkip", "copyTo.modeReplace" }) _mode.Items.Add(Loc.Current[key]);
        var cancel = new Button { Name = "CopyCancel", IsCancel = true, Content = Loc.Current["explorer.cancel"] };
        _dependencies.IsCheckedChanged += (_, _) => _ = RefreshAsync();
        _mode.SelectionChanged += (_, _) => _ = RefreshAsync();
        _ok.IsDefault = true;
        _ok.Click += (_, _) => Close(_plan);
        cancel.Click += (_, _) => Close(null);
        Closed += (_, _) => _version = int.MaxValue;
        Opened += (_, _) => _ = RefreshAsync();
        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = Mods.ModsUi.Space(3),
            Children =
            {
                new TextBlock { Text = Loc.Current.Format("copyTo.header", ("count", items.Count), ("source", source.Name), ("target", target.Name)), TextWrapping = TextWrapping.Wrap },
                _dependencies,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = Loc.Current["copyTo.mode"], VerticalAlignment = VerticalAlignment.Center }, _mode } },
                _list,
                _notes,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { _ok, cancel } },
            },
        };
    }

    private CopyPlan? _plan;

    private CollisionMode DefaultMode => _mode.SelectedIndex switch { 1 => CollisionMode.Skip, 2 => CollisionMode.Replace, _ => CollisionMode.Rename };

    private async Task RefreshAsync()
    {
        if (_rendering) return;
        var version = ++_version;
        _ok.IsEnabled = false;
        _notes.Text = Loc.Current["copyTo.planning"];
        var plan = await ResourceCopier.PlanAsync(_source, _target, _items, _dependencies.IsChecked == true, DefaultMode, _overrides, CancellationToken.None);
        // A newer choice, or closing the window, makes this answer stale.
        if (version == _version) Render(plan);
    }

    private void Render(CopyPlan plan)
    {
        _plan = plan;
        _rendering = true;
        _list.ItemsSource = plan.Entries.Select(Row).ToList();
        _rendering = false;
        var counts = Loc.Current.Format("copyTo.counts", ("copy", plan.Applied.Count()), ("skip", plan.Entries.Count(e => e.Action == CopyAction.Skip)), ("refused", plan.Entries.Count(e => e.Action == CopyAction.Refused)));
        var notes = plan.Refusal is not null ? [plan.Refusal] : plan.Warnings.Prepend(plan.CanApply ? counts : Loc.Current["copyTo.nothing"]);
        _notes.Text = string.Join(Environment.NewLine, notes);
        _ok.IsEnabled = plan.CanApply;
    }

    private Control Row(PlanEntry entry)
    {
        var label = $"{Loc.Current["explorer.type." + entry.Type]}  {entry.Name}" + (entry.Action == CopyAction.Rename ? $"  >  {entry.TargetName}" : "")
            + (entry.IsDependency && entry.UsedBy.Count > 0 ? "  (" + Loc.Current.Format("copyTo.neededBy", ("names", string.Join(", ", entry.UsedBy))) + ")" : "")
            + (entry.Refusal is null ? "" : "  " + entry.Refusal);
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 430 };
        if (entry.Refusal is not null) text.Foreground = (IBrush?)Avalonia.Application.Current?.FindResource("Danger");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Choice(entry), text } };
        return row;
    }

    // A name that exists in the target gets a choice; any other item only shows what happens to it.
    private Control Choice(PlanEntry entry)
    {
        if (!entry.Collides || entry.Refusal is not null) return new TextBlock { Text = Loc.Current[ActionKey(entry.Action)], Width = 110, VerticalAlignment = VerticalAlignment.Center };
        var box = new ComboBox { Width = 110 };
        box.Items.Add(Loc.Current["copyTo.choiceRename"]);
        box.Items.Add(Loc.Current["copyTo.actionSkip"]);
        if (entry.SameType) box.Items.Add(Loc.Current["copyTo.actionReplace"]);
        box.SelectedIndex = entry.Action switch { CopyAction.Skip => 1, CopyAction.Replace => 2, _ => 0 };
        box.SelectionChanged += (_, _) =>
        {
            if (_rendering) return;
            _overrides[entry.Key] = box.SelectedIndex switch { 1 => CollisionMode.Skip, 2 => CollisionMode.Replace, _ => CollisionMode.Rename };
            _ = RefreshAsync();
        };
        return box;
    }

    private static string ActionKey(CopyAction action) => action switch
    {
        CopyAction.Skip => "copyTo.actionSkip",
        CopyAction.Replace => "copyTo.actionReplace",
        CopyAction.Refused => "copyTo.actionRefused",
        _ => "copyTo.actionAdd",
    };

    /// <summary>The plan the user accepted, or null.</summary>
    public async Task<CopyPlan?> AskAsync(Window owner) => await ShowDialog<CopyPlan?>(owner);
}
