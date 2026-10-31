using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Localization;
using G3MStudio.Localization;

namespace G3MStudio;

public sealed record PaletteItem(string Id, string Title, string Shortcut);

/// <summary>Minimal command palette: substring filter over the titles of enabled commands. Fuzzy matching and the other modes come later (design/shell.md 3.2).</summary>
public partial class PaletteWindow : Window
{
    private readonly CommandRegistry _registry;
    private readonly Localizer _loc;

    public PaletteWindow() : this(new CommandRegistry(), Loc.Current) { }

    public PaletteWindow(CommandRegistry registry, Localizer loc)
    {
        _registry = registry;
        _loc = loc;
        InitializeComponent();
        Input.TextChanged += (_, _) => Refresh();
        Input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        Results.DoubleTapped += (_, _) => RunSelected();
        Opened += (_, _) => Input.Focus();
        Refresh();
    }

    public IReadOnlyList<PaletteItem> Items => [.. Results.Items.OfType<PaletteItem>()];

    private void Refresh()
    {
        var text = Input.Text?.Trim() ?? "";
        var items = _registry.All
            .Where(_registry.IsEnabled)
            .Select(c => new PaletteItem(c.Id, c.Title ?? _loc[AppCommands.TitleKey(c.Id)], Shortcuts.Parse(c.Shortcut)?.ToString() ?? ""))
            .Where(i => i.Title.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(i => i.Title.StartsWith(text, StringComparison.CurrentCultureIgnoreCase))
            .ThenBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        Results.ItemsSource = items;
        Results.SelectedIndex = items.Count > 0 ? 0 : -1;
        Empty.IsVisible = items.Count == 0;
        Empty.Text = _loc.Format("palette.empty", ("text", text));
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down: Move(1); break;
            case Key.Up: Move(-1); break;
            case Key.Enter: RunSelected(); break;
            case Key.Escape: Close(); break;
            default: return;
        }

        e.Handled = true;
    }

    private void Move(int step)
    {
        var count = Results.ItemCount;
        if (count > 0) Results.SelectedIndex = (Results.SelectedIndex + step + count) % count;
    }

    private void RunSelected()
    {
        if (Results.SelectedItem is not PaletteItem item) return;
        Close();
        _registry.Execute(item.Id);
    }
}
