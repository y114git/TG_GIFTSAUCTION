using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>
/// Renames many resources by a pattern (prefix, suffix, replace, regular expression). The list shows every old and new name and the reason a
/// name cannot run before anything is changed; Apply runs the names that can as one undo step.
/// </summary>
public sealed class BulkRenameWindow : Window
{
    private const int ShownLines = 300;

    private static readonly RenameMode[] Modes = [RenameMode.Prefix, RenameMode.Suffix, RenameMode.Replace, RenameMode.Regex];

    private readonly IReadOnlyList<(string Type, string Name)> _items;
    private readonly Func<string, string, bool> _exists;
    private readonly ComboBox _mode = new() { Name = "BulkMode", MinWidth = 160 };
    private readonly TextBox _find = new() { Name = "BulkFind", MinWidth = 160 };
    private readonly TextBox _text = new() { Name = "BulkText", MinWidth = 160 };
    private readonly ListBox _preview = new() { Name = "BulkPreview", MinHeight = 160, MaxHeight = 320 };
    private readonly TextBlock _summary = new() { Name = "BulkSummary", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _apply = new() { Name = "BulkApply", IsEnabled = false };
    private IReadOnlyList<RenameStep> _steps = [];

    /// <param name="items">The resources to rename.</param>
    /// <param name="exists">Whether a resource of that type already has that name.</param>
    /// <param name="apply">Runs the steps; returns the reason it was refused, or null when done.</param>
    public BulkRenameWindow(IReadOnlyList<(string Type, string Name)> items, Func<string, string, bool> exists, Func<IReadOnlyList<RenameStep>, string?> apply)
    {
        (_items, _exists) = (items, exists);
        Title = Loc.Current.Format("organize.bulkTitle", ("count", items.Count));
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _mode.ItemsSource = Modes.Select(m => Loc.Current["organize.mode." + m.ToString().ToLowerInvariant()]).ToList();
        _mode.SelectedIndex = 0;
        _apply.Content = Loc.Current["organize.bulkApply"];
        _apply.IsDefault = true;
        var cancel = new Button { Name = "BulkCancel", IsCancel = true, Content = Loc.Current["explorer.cancel"] };
        var error = new TextBlock { Name = "BulkError", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = (Avalonia.Media.IBrush?)Application.Current?.FindResource("Danger") };
        _find.PlaceholderText = Loc.Current["organize.find"];
        _text.PlaceholderText = Loc.Current["organize.text"];
        _mode.Labeled("organize.bulkMode");
        _find.Labeled("organize.find");
        _text.Labeled("organize.text");
        _preview.Labeled("organize.bulkPreview");
        _mode.SelectionChanged += (_, _) => Refresh();
        _find.TextChanged += (_, _) => Refresh();
        _text.TextChanged += (_, _) => Refresh();
        _apply.Click += (_, _) =>
        {
            var refused = apply(_steps);
            error.Text = refused ?? "";
            if (refused is null) Close(true);
        };
        cancel.Click += (_, _) => Close(false);
        var space = Application.Current is { } app && app.TryGetResource("Space.3", app.ActualThemeVariant, out var value) && value is double d ? d : 12;
        Content = new StackPanel
        {
            Margin = new Thickness(space),
            Spacing = space,
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = space / 2, Children = { _mode, _find, _text } },
                _summary,
                _preview,
                error,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = space / 2, HorizontalAlignment = HorizontalAlignment.Right, Children = { _apply, cancel } },
            },
        };
        Opened += (_, _) => _text.Focus();
        Refresh();
    }

    private RenameSpec Spec => new(Modes[Math.Max(0, _mode.SelectedIndex)], _find.Text ?? "", _text.Text ?? "");

    private void Refresh()
    {
        // Prefix and suffix have nothing to find.
        _find.IsEnabled = Spec.Mode is RenameMode.Replace or RenameMode.Regex;
        _steps = BulkRename.Plan(_items, Spec, _exists);
        var runs = _steps.Count(s => s.Runs);
        var problems = _steps.Count(s => s.Problem is not null);
        _summary.Text = Loc.Current.Format("organize.bulkSummary", ("count", runs), ("problems", problems), ("same", _steps.Count - runs - problems));
        _preview.ItemsSource = _steps.Take(ShownLines).Select(Line).ToList();
        _apply.IsEnabled = runs > 0;
    }

    private static string Line(RenameStep s) =>
        !s.Changes && s.Problem is null ? s.Old : $"{s.Old}  >  {s.New}" + (s.Problem is null ? "" : "   [" + Loc.Current["organize.problem." + s.Problem] + "]");

    /// <summary>True when the names were changed.</summary>
    public async Task<bool> AskAsync(Window owner) => await ShowDialog<bool?>(owner) ?? false;
}
