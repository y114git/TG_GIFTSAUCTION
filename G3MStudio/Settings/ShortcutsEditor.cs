using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Settings;
using G3MStudio.Localization;

namespace G3MStudio.Settings;

/// <summary>Keyboard shortcuts (design/settings.md): every command with its shortcut, a filter, press-a-key to change, reset to the default.</summary>
public sealed class ShortcutsEditor : UserControl
{
    private readonly CommandRegistry _registry;
    private readonly SettingsStore _store;
    private readonly TextBox _filter = new() { Name = "ShortcutFilter", PlaceholderText = Loc.Current["settings.shortcutFilter"] };
    private readonly StackPanel _rows = new() { Spacing = 2 };
    private readonly TextBlock _note = new() { Name = "ShortcutNote", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private string? _capturing;

    /// <summary>True while a shortcut waits for its keys; the window must not run commands then.</summary>
    public static bool Capturing { get; private set; }

    public ShortcutsEditor(CommandRegistry registry, SettingsStore store)
    {
        (_registry, _store) = (registry, store);
        Name = "ShortcutsEditor";
        Content = new StackPanel { Spacing = 6, Children = { _filter, _note, _rows } };
        _filter.TextChanged += (_, _) => Fill();
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        DetachedFromVisualTree += (_, _) => Capturing = false;
        Fill();
    }

    /// <summary>The registry's shortcut text for a key press: Ctrl stands for Cmd on macOS, like the registry's own shortcuts.</summary>
    public static string? Describe(KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None) return null;
        var parts = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Ctrl");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        parts.Add(e.Key.ToString());
        return string.Join('+', parts);
    }

    private void Fill()
    {
        _rows.Children.Clear();
        Capturing = _capturing is not null;
        Button? focus = null;
        var text = _filter.Text?.Trim() ?? "";
        foreach (var command in _registry.All.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var title = Loc.Current["cmd." + command.Id];
            if (text.Length > 0 && !title.Contains(text, StringComparison.CurrentCultureIgnoreCase) && !command.Id.Contains(text, StringComparison.OrdinalIgnoreCase) && command.Shortcut?.Contains(text, StringComparison.OrdinalIgnoreCase) != true) continue;
            var id = command.Id;
            var capture = new Button { Name = "Shortcut_" + id, MinWidth = 160, Content = _capturing == id ? Loc.Current["settings.pressKeys"] : command.Shortcut ?? Loc.Current["settings.noShortcut"] };
            capture.Click += (_, _) => { _capturing = id; Fill(); };
            if (_capturing == id) focus = capture;
            var reset = new Button { Name = "ShortcutReset_" + id, Content = Loc.Current["settings.reset"], IsEnabled = command.Shortcut != _registry.DefaultShortcut(id) };
            reset.Click += (_, _) => Set(id, _registry.DefaultShortcut(id));
            _rows.Children.Add(new DockPanel { Children = { new StackPanel { [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right, Orientation = Orientation.Horizontal, Spacing = 6, Children = { capture, reset } }, new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center } } });
        }

        // The rows were rebuilt, so the key press must reach the new button.
        if (focus is not null) Avalonia.Threading.Dispatcher.UIThread.Post(() => focus.Focus());
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_capturing is not { } id) return;
        e.Handled = true;
        if (e.Key == Key.Escape) { _capturing = null; Fill(); return; }
        if (e.Key is Key.Back or Key.Delete && e.KeyModifiers == KeyModifiers.None) { Set(id, null); return; }
        if (Describe(e) is { } shortcut) Set(id, shortcut);
    }

    private void Set(string id, string? shortcut)
    {
        (_capturing, Capturing) = (null, false);
        var others = _registry.All.Where(c => c.Id != id && shortcut is not null && c.Shortcut == shortcut).Select(c => Loc.Current["cmd." + c.Id]).ToList();
        _note.Text = others.Count > 0 ? Loc.Current.Format("settings.shortcutShared", ("shortcut", shortcut!), ("commands", string.Join(", ", others))) : "";
        var isDefault = shortcut == _registry.DefaultShortcut(id);
        _store.Update(s => s with { Shortcuts = isDefault ? s.Shortcuts.Where(p => p.Key != id).ToDictionary() : new Dictionary<string, string>(s.Shortcuts) { [id] = shortcut ?? "" } });
        Fill();
    }
}
