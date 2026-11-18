using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Nicknames: the interface shows them where it shows a resource name; the real name stays the identity behind every action.</summary>
public sealed partial class MainWindow
{
    private Nicknames? _nicknames;

    // One-time hookup of the places that build their own text from a name.
    private void WireNicknames()
    {
        Docking.TitleOf = TabLabel;
        _usages.OwnerLabel = hit => Workspaces.Current?.Nicknames.OwnerLabel(hit.OwnerType, hit.OwnerName) ?? hit.OwnerName;
        _problems.SourceLabel = source => Workspaces.Current?.Nicknames.CodeLabel(source) ?? source;
        _problems.MessageLabel = message => Workspaces.Current?.Nicknames.Decorate(message) ?? message;
        _search.EntryLabel = entry => Workspaces.Current?.Nicknames.CodeLabel(entry) ?? entry;
    }

    /// <summary>Follows the nicknames of the open file; null when none is open.</summary>
    private void BindNicknames(Workspace? workspace)
    {
        _nicknames?.Changed -= OnNicknamesChanged;
        _nicknames?.SaveFailed -= OnNicknamesSaveFailed;
        _nicknames = workspace?.Nicknames;
        _nicknames?.Changed += OnNicknamesChanged;
        _nicknames?.SaveFailed += OnNicknamesSaveFailed;
        Explorer.Model.Nicknames = _nicknames ?? new Nicknames();
    }

    // The nickname of a resource of a known type, for texts of the main window.
    private string Shown(string type, string name) => Workspaces.Current?.Nicknames.Label(type, name) ?? name;

    // A code entry as the interface shows it: the nickname of its owner inside its name.
    private string CodeShown(string entry) => Workspaces.Current?.Nicknames.CodeLabel(entry) ?? entry;

    private string? TabLabel(TabInfo tab)
    {
        if (tab.Key == ResourceTypes.KeyOf("general", ResourceTypes.GeneralInfoName)) return Loc.Current["explorer.generalName"];
        if (Workspaces.Current?.Nicknames is not { Any: true } nicknames) return null;
        var name = tab.Key[(tab.Key.IndexOf(':', StringComparison.Ordinal) + 1)..];
        var label = tab.Kind == "code" ? nicknames.CodeLabel(name) : nicknames.Label(tab.Key);
        return label == name ? null : label;
    }

    private void OnNicknamesSaveFailed(string reason) => Notify(NotificationLevel.Error, Loc.Current.Format("rename.nicknameSaveFailed", ("reason", reason)));

    private void OnNicknamesChanged()
    {
        Explorer.Refresh();
        Docking.RetitleTabs();
        _history.Relabel();
        _problems.Relabel();
        foreach (var room in _roomViews.Values) room.RefreshNames();
        _inspected = null; // shown again below with the new title
        ShowInInspector();
        UpdateTexts(); // the window title follows the tab title
    }
}
