using Avalonia.Controls;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

public enum DiskChangedChoice { Overwrite, SaveCopy, Cancel }

/// <summary>Asked when Save would replace a data file that another program changed: Overwrite, Save As (a copy of the user's version) or Cancel.</summary>
public sealed class DiskChangedWindow : Window
{
    public DiskChangedWindow(string name)
    {
        Title = Loc.Current["save.askTitle"];
        Width = 440;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        Button Choice(string key, DiskChangedChoice choice, string id, DialogRole role) => DialogFrame.Action(this, Loc.Current[key], id, () => Close(choice), role);
        DialogFrame.Show(this, new TextBlock { Text = Loc.Current.Format("save.changed", ("name", name)), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            [Choice("save.overwrite", DiskChangedChoice.Overwrite, "Overwrite", DialogRole.Normal), Choice("cmd.file.saveAs", DiskChangedChoice.SaveCopy, "SaveAs", DialogRole.Primary), Choice("common.cancel", DiskChangedChoice.Cancel, "Cancel", DialogRole.Cancel)],
            scroll: DialogScroll.Vertical);
    }

    /// <summary>Closing the dialog with its own close button counts as Cancel.</summary>
    public async Task<DiskChangedChoice> AskAsync(Window owner) => await ShowDialog<DiskChangedChoice?>(owner) ?? DiskChangedChoice.Cancel;
}
