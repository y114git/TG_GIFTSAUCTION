using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>
/// The one entry for moving resources between data files, folders and packages: the ways in three groups, each the command it names. Choosing runs that command,
/// so every way keeps its own dialog and behavior; the command ids stay in the palette and the shortcuts.
/// </summary>
public sealed class TransferWindow : Window
{
    /// <summary>The ways in order: into this file, out of it, then mods.</summary>
    public static readonly string[] Modes = Core.Commands.AppCommands.TransferModes;

    private readonly ListBox _modes = new() { Name = "TransferModes" };

    public TransferWindow()
    {
        Title = Loc.Current["transfer.title"];
        MinWidth = 420;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _modes.Labeled("cmd.file.transfer");
        var muted = (IBrush?)Application.Current?.FindResource("Text.Muted");
        foreach (var (titleKey, ids) in Core.Commands.AppCommands.TransferGroups)
        {
            _modes.Items.Add(new ListBoxItem { IsEnabled = false, Focusable = false, Content = new TextBlock { Text = Loc.Current[titleKey], FontWeight = FontWeight.SemiBold } });
            foreach (var id in ids)
            {
                var label = Loc.Current["cmd." + id];
                var caption = new TextBlock { Text = Loc.Current["transfer.about." + id[(id.IndexOf('.', StringComparison.Ordinal) + 1)..]], TextWrapping = TextWrapping.Wrap, Foreground = muted }.TextSize("FontSize.Small");
                var item = new ListBoxItem { Tag = id, Content = new StackPanel { Children = { new TextBlock { Text = label }, caption } } };
                AutomationProperties.SetName(item, label);
                _modes.Items.Add(item);
            }
        }

        Select(0);
        var ok = DialogFrame.Wire(this, new Button { Name = "TransferOk", Content = Loc.Current["transfer.continue"] }, Choose, DialogRole.Primary);
        _modes.DoubleTapped += (_, _) => { if (Selected is not null) DialogFrame.Commit(this, Choose); };
        Opened += (_, _) =>
        {
            if (Screens.ScreenFromWindow(this) is { } screen) MaxHeight = screen.WorkingArea.Height / screen.Scaling * 0.9;
        };
        DialogFrame.Show(this, _modes, [ok, DialogFrame.Cancel(this, () => Close(null), "TransferCancel")], scroll: DialogScroll.Vertical);
    }

    /// <summary>The command id of the highlighted way (what Continue closes with).</summary>
    public string? Selected => (_modes.SelectedItem as ListBoxItem)?.Tag as string;

    public void Select(int index) => _modes.SelectedItem = _modes.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Tag as string == Modes[index]);

    private void Choose() => Close(Selected);
}
