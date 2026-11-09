using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>The Layout menu: built-in presets, named layouts saved in the settings, folding the docks, Show All and Close All Windows.</summary>
public sealed partial class MainWindow
{
    public void ResetLayout()
    {
        Docking.Reset();
        SaveView();
    }

    /// <summary>A built-in preset by id (studio, wide, code, canvas, minimal) or a saved layout by name.</summary>
    public void UseLayoutPreset(string preset)
    {
        if (!LayoutBook.IsBuiltIn(preset))
        {
            LoadLayout(preset);
            return;
        }

        if (preset == "studio") Docking.Reset();
        else Docking.ApplyPreset(preset);
        SaveView();
    }

    private void LoadLayout(string name)
    {
        if (!Workspaces.Settings.Current.SavedLayouts.TryGetValue(name, out var json)) return;
        try
        {
            Docking.ApplyLayout(json);
        }
        catch (FormatException)
        {
            Notify(NotificationLevel.Warning, Loc.Current["layouts.broken"]);
        }
    }

    private string? StoreLayout(string name)
    {
        if (LayoutBook.Clean(name) is not { } clean) return Loc.Current["layouts.invalidName"];
        Workspaces.Settings.Update(x => x with { SavedLayouts = LayoutBook.Save(x.SavedLayouts, clean, Docking.Save()) });
        return null;
    }

    public void SaveLayout() => _ = new RenameWindow("", StoreLayout, Loc.Current["layouts.save"], Loc.Current["layouts.name"], Loc.Current["layouts.saveOk"]).AskAsync(this);

    public void ShowLayouts()
    {
        var settings = Workspaces.Settings;
        new LayoutsWindow(
            () => LayoutBook.Names(settings.Current.SavedLayouts),
            StoreLayout,
            LoadLayout,
            name => settings.Update(x => x with { SavedLayouts = LayoutBook.Delete(x.SavedLayouts, name) }),
            (from, to) =>
            {
                // Rename trims the name, so the check runs on the trimmed one too.
                if (LayoutBook.Clean(to) is not { } clean || clean != from && settings.Current.SavedLayouts.ContainsKey(clean)) return Loc.Current["layouts.invalidName"];
                settings.Update(x => x with { SavedLayouts = LayoutBook.Rename(x.SavedLayouts, from, clean) });
                return null;
            }).Show(this);
    }

    public void ToggleDocks()
    {
        Docking.ToggleAll();
        SaveView();
    }

    public void ToggleDock(string side)
    {
        Docking.ToggleDock(side);
        SaveView();
    }

    public void ShowAllWindows()
    {
        Docking.ShowAll();
        SaveView();
    }

    public void CloseAllWindows()
    {
        foreach (var doc in Docking.Documents().ToList()) Docking.CloseTab(doc.Id);
    }
}
