using G3MStudio.Core.Notifications;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>The layout commands of the Window menu: reset, and named layouts that are saved in the settings.</summary>
public sealed partial class MainWindow
{
    public void ResetLayout()
    {
        Docking.Reset();
        SaveView();
    }

    public void ShowLayouts()
    {
        var settings = Workspaces.Settings;
        new LayoutsWindow(
            () => [.. settings.Current.SavedLayouts.Keys.Order(StringComparer.CurrentCultureIgnoreCase)],
            name => settings.Update(x => x with { SavedLayouts = new Dictionary<string, string>(x.SavedLayouts) { [name] = Docking.Save() } }),
            name =>
            {
                if (!settings.Current.SavedLayouts.TryGetValue(name, out var json)) return;
                try
                {
                    Docking.ApplyLayout(json);
                }
                catch (FormatException)
                {
                    Notify(NotificationLevel.Warning, Loc.Current["layouts.broken"]);
                }
            },
            name => settings.Update(x => x with { SavedLayouts = x.SavedLayouts.Where(p => p.Key != name).ToDictionary() })).Show(this);
    }
}
