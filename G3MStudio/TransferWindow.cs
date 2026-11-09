using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>
/// The one entry for moving resources between data files, folders and packages: a list of the ways, each the command it names. Choosing runs that command,
/// so every way keeps its own dialog and behavior and none is lost; the command ids stay in the palette and the Explorer menu.
/// </summary>
public sealed class TransferWindow : Window
{
    /// <summary>The ways in order: into this file, out of it, then mods.</summary>
    public static readonly string[] Modes = Core.Commands.AppCommands.TransferModes;

    private readonly ListBox _modes = new() { Name = "TransferModes", SelectedIndex = 0 };

    public TransferWindow()
    {
        Title = Loc.Current["cmd.file.transfer"];
        Width = 460;
        Height = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _modes.Labeled("cmd.file.transfer");
        _modes.ItemsSource = Modes.Select(id => Loc.Current["cmd." + id]).ToList();
        var ok = new Button { Name = "TransferOk", Content = Loc.Current["transfer.ok"], IsDefault = true };
        var cancel = new Button { Name = "TransferCancel", Content = Loc.Current["explorer.cancel"], IsCancel = true };
        ok.Click += (_, _) => Choose();
        cancel.Click += (_, _) => Close(null);
        _modes.DoubleTapped += (_, _) => Choose();
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0), Children = { ok, cancel } };
        var panel = new DockPanel { Margin = new Thickness(12), LastChildFill = true };
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Bottom);
        panel.Children.Add(bar);
        panel.Children.Add(_modes);
        Content = panel;
    }

    /// <summary>The command id of the highlighted way (what OK closes with).</summary>
    public string? Selected => _modes.SelectedIndex is >= 0 and var i && i < Modes.Length ? Modes[i] : null;

    public void Select(int index) => _modes.SelectedIndex = index;

    private void Choose() => Close(Selected);
}
