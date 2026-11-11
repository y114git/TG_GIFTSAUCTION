using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Settings;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Settings;

/// <summary>Keyboard shortcuts (design/settings.md): every command with its gestures (the primary and any aliases), a filter, press-a-key to change, add or remove an alias, reset to the default.</summary>
public sealed class ShortcutsEditor : UserControl
{
    private readonly CommandRegistry _registry;
    private readonly SettingsStore _store;
    private readonly TextBox _filter = new() { Name = "ShortcutFilter", PlaceholderText = Loc.Current["settings.shortcutFilter"] };
    private readonly StackPanel _rows = new() { Spacing = 2 };
    private readonly TextBlock _note = new() { Name = "ShortcutNote", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private string? _capturing;
    private int _slot; // 0 is the primary gesture, k the alias k, AddSlot a new alias
    private const int AddSlot = int.MaxValue;

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
            var title = command.Title ?? MainWindow.PaletteTitle(command.Id, Loc.Current);
            if (text.Length > 0 && !title.Contains(text, StringComparison.CurrentCultureIgnoreCase) && !command.Id.Contains(text, StringComparison.OrdinalIgnoreCase) && !command.Gestures.Any(g => g.Contains(text, StringComparison.OrdinalIgnoreCase) || G3MStudio.Commands.Shortcuts.Display(g).Contains(text, StringComparison.OrdinalIgnoreCase))) continue;
            var id = command.Id;
            var buttons = new StackPanel { [Grid.ColumnProperty] = 1, Orientation = Orientation.Horizontal, Spacing = 8 };
            Button Slot(string name, int slot, string label)
            {
                var button = new Button { Name = name, MinWidth = 128, Content = _capturing == id && _slot == slot ? Loc.Current["settings.pressKeys"] : G3MStudio.Commands.Shortcuts.Display(label) };
                if (slot == AddSlot && _capturing != id) // a plain "+" button, not a field-like box
                {
                    button.ClearValue(MinWidthProperty); // the icon style gives it the control height as its width
                    button.Content = "+";
                    button.Classes.Add("icon");
                    button.Labeled("settings.addShortcut");
                }

                button.Click += (_, _) => { (_capturing, _slot) = (id, slot); Fill(); };
                if (_capturing == id && _slot == slot) focus = button;
                return button;
            }

            buttons.Children.Add(Slot("Shortcut_" + id, 0, command.Shortcut ?? Loc.Current["settings.noShortcut"]));
            var aliases = command.Aliases ?? [];
            for (var i = 0; i < aliases.Count; i++) buttons.Children.Add(Slot($"ShortcutAlias_{id}_{i + 1}", i + 1, aliases[i]));
            buttons.Children.Add(Slot("ShortcutAdd_" + id, AddSlot, "+"));
            var isDefault = command.Shortcut == _registry.DefaultShortcut(id) && aliases.SequenceEqual(_registry.DefaultAliases(id));
            var reset = new Button { Name = "ShortcutReset_" + id, Content = Loc.Current["settings.reset"], IsEnabled = !isDefault };
            reset.Click += (_, _) => Apply(id, _registry.DefaultShortcut(id), _registry.DefaultAliases(id));
            buttons.Children.Add(reset);
            // The title takes the width the keys leave, cut with an ellipsis and complete in its tooltip.
            var label = new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis, Margin = new Avalonia.Thickness(0, 0, 8, 0) };
            ToolTip.SetTip(label, title);
            _rows.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { label, buttons } });
        }

        // The rows were rebuilt, so the key press must reach the new button.
        if (focus is not null) Avalonia.Threading.Dispatcher.UIThread.Post(() => focus.Focus());
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_capturing is not { } id) return;
        e.Handled = true;
        if (e.Key == Key.Escape) { _capturing = null; Fill(); return; }
        if (e.Key is Key.Back or Key.Delete && e.KeyModifiers == KeyModifiers.None) { Set(id, _slot, null); return; }
        if (Describe(e) is { } shortcut) Set(id, _slot, shortcut);
    }

    /// <summary>Puts a gesture (or none, which removes an alias or clears the primary) into a slot of a command.</summary>
    public void Set(string id, int slot, string? shortcut)
    {
        var command = _registry.Get(id);
        var primary = command.Shortcut;
        var aliases = (command.Aliases ?? []).ToList();
        if (slot == 0) primary = shortcut;
        else if (slot == AddSlot) { if (shortcut is not null) aliases.Add(shortcut); }
        else if (shortcut is null) aliases.RemoveAt(slot - 1);
        else aliases[slot - 1] = shortcut;
        Apply(id, primary, aliases, shortcut);
    }

    private void Apply(string id, string? primary, IReadOnlyList<string> aliases, string? changed = null)
    {
        (_capturing, Capturing) = (null, false);
        aliases = [.. aliases.Where(a => !string.Equals(a, primary, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase)];
        var others = changed is null ? [] : _registry.UsersOf(changed, id).Select(c => c.Title ?? MainWindow.PaletteTitle(c.Id, Loc.Current)).ToList();
        _note.Text = others.Count > 0 ? Loc.Current.Format("settings.shortcutShared", ("shortcut", changed ?? ""), ("commands", string.Join(", ", others))) : "";
        var sameAliases = aliases.SequenceEqual(_registry.DefaultAliases(id));
        _store.Update(s => s with
        {
            Shortcuts = primary == _registry.DefaultShortcut(id) ? s.Shortcuts.Where(p => p.Key != id).ToDictionary() : new Dictionary<string, string>(s.Shortcuts) { [id] = primary ?? "" },
            ShortcutAliases = sameAliases ? s.ShortcutAliases.Where(p => p.Key != id).ToDictionary() : new Dictionary<string, string[]>(s.ShortcutAliases) { [id] = [.. aliases] },
        });
        Fill();
    }
}
