using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>Asked when an image editor tab closes with edits that are not applied: Apply (Save), Discard (DoItLater) or Cancel.</summary>
public sealed class ImageChangesWindow : Window
{
    public ImageChangesWindow(string name, bool conflict = false)
    {
        Title = Loc.Current["image.apply"];
        Width = 440;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        Button Choice(string key, CloseChoice choice, string id, DialogRole role) => DialogFrame.Action(this, Loc.Current[key], id, () => Close(choice), role);
        DialogFrame.Show(this, new TextBlock { Text = Loc.Current.Format(conflict ? "image.conflictAsk" : "image.closeAsk", ("name", name)), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            [Choice(conflict ? "image.applyAnyway" : "image.apply", CloseChoice.Save, "Apply", DialogRole.Primary), Choice(conflict ? "image.discardMine" : "image.discard", CloseChoice.DoItLater, "Discard", DialogRole.Normal), Choice("common.cancel", CloseChoice.Cancel, "Cancel", DialogRole.Cancel)],
            scroll: DialogScroll.Vertical);
    }

    /// <summary>Closing the dialog with its own close button counts as Cancel.</summary>
    public async Task<CloseChoice> AskAsync(Window owner) => await ShowDialog<CloseChoice?>(owner) ?? CloseChoice.Cancel;
}
