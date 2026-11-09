using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Settings;

public sealed partial class SettingsPage
{
    private static readonly ExplorerSort[] SortOrders = [ExplorerSort.ListOrder, ExplorerSort.NameAscending, ExplorerSort.NameDescending];

    /// <summary>The Explorer options: default view, counts, folder behaviour and the sort order of files without a saved view.</summary>
    private Control[] ExplorerSection(Core.Settings.AppSettings s)
    {
        var folderView = new CheckBox { Name = "ExplorerFolderView", Tag = Loc.Current["settings.explorerFolderView"], Content = Loc.Current["settings.explorerFolderView"], IsChecked = s.ExplorerFolderView };
        folderView.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { ExplorerFolderView = folderView.IsChecked == true }); };
        var counts = new CheckBox { Name = "ExplorerShowCounts", Tag = Loc.Current["settings.explorerShowCounts"], Content = Loc.Current["settings.explorerShowCounts"], IsChecked = s.ExplorerShowCounts };
        counts.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { ExplorerShowCounts = counts.IsChecked == true }); };
        var open = new CheckBox { Name = "ExplorerFoldersOpen", Tag = Loc.Current["settings.explorerFoldersOpen"], Content = Loc.Current["settings.explorerFoldersOpen"], IsChecked = s.ExplorerFoldersOpen };
        open.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { ExplorerFoldersOpen = open.IsChecked == true }); };
        var sort = new ComboBox
        {
            Name = "ExplorerSortOrder",
            ItemsSource = new[] { Loc.Current["explorer.sortList"], Loc.Current["explorer.sortAz"], Loc.Current["explorer.sortZa"] },
            SelectedIndex = Math.Max(0, Array.IndexOf(SortOrders, s.ExplorerSortOrder)),
            MinWidth = Wide(15),
        };
        sort.SelectionChanged += (_, _) => { if (!_building && sort.SelectedIndex >= 0) _store.Update(x => x with { ExplorerSortOrder = SortOrders[sort.SelectedIndex] }); };
        return [folderView, counts, open, Row("settings.explorerSort", sort)];
    }
}
