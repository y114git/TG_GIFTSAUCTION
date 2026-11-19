using Avalonia.Controls;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Settings;

public sealed partial class SettingsPage
{
    // A row with a choice of showing, graying out or hiding one kind of Explorer entry; the label is also what the settings search matches.
    private StackPanel Visibility(string name, string labelKey, EntryVisibility current, Func<AppSettings, EntryVisibility, AppSettings> set)
    {
        var box = EntryVisibilityBox.Create(name, current, v => { if (!_building) _store.Update(x => set(x, v)); });
        box.MinWidth = Wide(10);
        return Row(labelKey, box);
    }

    private static readonly ExplorerSort[] SortOrders = [ExplorerSort.ListOrder, ExplorerSort.NameAscending, ExplorerSort.NameDescending];

    /// <summary>The Explorer options: default view, counts, folder behaviour, the sort order of files without a saved view and the size of the row pictures.</summary>
    private Control[] ExplorerSection(Core.Settings.AppSettings s)
    {
        var folderView = new CheckBox { Name = "ExplorerFolderView", Tag = Loc.Current["settings.explorerFolderView"], Content = Loc.Current["settings.explorerFolderView"], IsChecked = s.ExplorerFolderView };
        folderView.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { ExplorerFolderView = folderView.IsChecked == true }); };
        var counts = new CheckBox { Name = "ExplorerShowCounts", Tag = Loc.Current["settings.explorerShowCounts"], Content = Loc.Current["settings.explorerShowCounts"], IsChecked = s.ExplorerShowCounts };
        counts.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { ExplorerShowCounts = counts.IsChecked == true }); };
        var open = new CheckBox { Name = "ExplorerFoldersOpen", Tag = Loc.Current["settings.explorerFoldersOpen"], Content = Loc.Current["settings.explorerFoldersOpen"], IsChecked = s.ExplorerFoldersOpen };
        open.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { ExplorerFoldersOpen = open.IsChecked == true }); };
        var hideCode = new CheckBox { Name = "ExplorerHideUndecompilable", Tag = Loc.Current["settings.explorerHideUndecompilable"], Content = Loc.Current["settings.explorerHideUndecompilable"], IsChecked = s.ExplorerHideUndecompilable };
        hideCode.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { ExplorerHideUndecompilable = hideCode.IsChecked == true }); };
        var hideBroken = new CheckBox { Name = "ExplorerHideBroken", Tag = Loc.Current["settings.explorerHideBroken"], Content = Loc.Current["settings.explorerHideBroken"], IsChecked = s.ExplorerHideBroken };
        hideBroken.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { ExplorerHideBroken = hideBroken.IsChecked == true }); };
        var anonymous = Visibility("ExplorerAnonymousFunctions", "settings.explorerAnonymous", s.ExplorerAnonymousFunctions, (x, v) => x with { ExplorerAnonymousFunctions = v });
        var structs = Visibility("ExplorerStructConstructors", "settings.explorerStructs", s.ExplorerStructConstructors, (x, v) => x with { ExplorerStructConstructors = v });
        var inner = Visibility("ExplorerInnerFunctions", "settings.explorerInner", s.ExplorerInnerFunctions, (x, v) => x with { ExplorerInnerFunctions = v });
        var sort = new ComboBox
        {
            Name = "ExplorerSortOrder",
            ItemsSource = new[] { Loc.Current["explorer.sortList"], Loc.Current["explorer.sortAz"], Loc.Current["explorer.sortZa"] },
            SelectedIndex = Math.Max(0, Array.IndexOf(SortOrders, s.ExplorerSortOrder)),
            MinWidth = Wide(15),
        };
        sort.SelectionChanged += (_, _) => { if (!_building && sort.SelectedIndex >= 0) _store.Update(x => x with { ExplorerSortOrder = SortOrders[sort.SelectedIndex] }); };
        var thumbSize = Number("ExplorerThumbnailSize", 16, 96, 2, s.ExplorerThumbnailSize, (x, n) => x with { ExplorerThumbnailSize = n });
        return [folderView, counts, open, hideCode, hideBroken, anonymous, structs, inner, Row("settings.explorerSort", sort), Row("settings.explorerIconSize", thumbSize, new TextBlock { Text = Loc.Current["settings.pixelUnit"], VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center })];
    }
}
