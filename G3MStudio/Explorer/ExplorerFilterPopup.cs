using Avalonia;
using Avalonia.Controls;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Explorer;

/// <summary>
/// The content of the Explorer filter popup: sort, groups first, the types to show and the tag to narrow to. Every control writes
/// the filter at once through <c>change</c>; the popup is built again each time it opens, so it shows the state in force.
/// </summary>
public static class ExplorerFilterPopup
{
    public static StackPanel Build(ExplorerFilter filter, IReadOnlyList<string> tags, string searchText, Action<Func<QuickAccess, QuickAccess>> change,
        AppSettings? settings = null, Action<Func<AppSettings, AppSettings>>? edit = null)
    {
        void Set(Func<ExplorerFilter, ExplorerFilter> edit) => change(q => q with { Filter = edit(q.Filter) });
        var panel = new StackPanel { Name = "ExplorerFilterPopup", Spacing = Space("Space.1"), MinWidth = 220, Margin = new Thickness(Space("Space.2")) };
        panel.Children.Add(Heading("explorer.sort"));
        foreach (var (sort, text) in new[] { (ExplorerSort.ListOrder, Loc.Current["explorer.sortList"]), (ExplorerSort.NameAscending, Loc.Current["explorer.sortAz"]), (ExplorerSort.NameDescending, Loc.Current["explorer.sortZa"]) })
        {
            var radio = new RadioButton { Content = text, IsChecked = filter.Sort == sort, GroupName = "ExplorerSort" };
            radio.IsCheckedChanged += (_, _) => { if (radio.IsChecked == true) Set(f => f with { Sort = sort }); };
            panel.Children.Add(radio);
        }

        var groups = new CheckBox { Name = "ExplorerGroupsFirst", Content = Loc.Current["explorer.groupsFirst"], IsChecked = filter.GroupsFirst };
        groups.IsCheckedChanged += (_, _) => Set(f => f with { GroupsFirst = groups.IsChecked == true });
        panel.Children.Add(groups);
        var empty = new CheckBox { Name = "ExplorerHideEmpty", Content = Loc.Current["explorer.hideEmpty"], IsChecked = filter.HideEmpty };
        empty.IsCheckedChanged += (_, _) => Set(f => f with { HideEmpty = empty.IsChecked == true });
        panel.Children.Add(empty);

        panel.Children.Add(Heading("explorer.typesHeading"));
        foreach (var type in ResourceTypes.All)
        {
            var box = new CheckBox { Content = Loc.Current["explorer.type." + type.Key], IsChecked = !filter.Hides(type.Key), Tag = type.Key };
            box.IsCheckedChanged += (_, _) => Set(f => f.Hides(type.Key) == (box.IsChecked == true) ? f.Toggle(type.Key) : f);
            panel.Children.Add(box);
        }

        if (settings is not null && edit is not null)
        {
            panel.Children.Add(Heading("explorer.codeHeading", center: true));
            panel.Children.Add(EntryVisibilityBox.Row("ExplorerAnonymousFunctions", "settings.explorerAnonymous", settings.ExplorerAnonymousFunctions, v => edit(x => x with { ExplorerAnonymousFunctions = v })));
            panel.Children.Add(EntryVisibilityBox.Row("ExplorerStructConstructors", "settings.explorerStructs", settings.ExplorerStructConstructors, v => edit(x => x with { ExplorerStructConstructors = v })));
            panel.Children.Add(EntryVisibilityBox.Row("ExplorerInnerFunctions", "settings.explorerInner", settings.ExplorerInnerFunctions, v => edit(x => x with { ExplorerInnerFunctions = v })));
            var undecompilable = new CheckBox { Name = "ExplorerHideUndecompilable", Content = Wrapped("settings.explorerHideUndecompilable"), IsChecked = settings.ExplorerHideUndecompilable };
            undecompilable.IsCheckedChanged += (_, _) => edit(x => x with { ExplorerHideUndecompilable = undecompilable.IsChecked == true });
            panel.Children.Add(undecompilable);
            var broken = new CheckBox { Name = "ExplorerHideBroken", Content = Wrapped("settings.explorerHideBroken"), IsChecked = settings.ExplorerHideBroken };
            broken.IsCheckedChanged += (_, _) => edit(x => x with { ExplorerHideBroken = broken.IsChecked == true });
            panel.Children.Add(broken);
        }

        if (tags.Count > 0)
        {
            panel.Children.Add(Heading("explorer.tagHeading"));
            foreach (var tag in tags.Prepend(""))
            {
                var radio = new RadioButton { Content = tag.Length == 0 ? Loc.Current["explorer.tagAny"] : tag, IsChecked = filter.Tag == tag, GroupName = "ExplorerTag" };
                radio.IsCheckedChanged += (_, _) => { if (radio.IsChecked == true) Set(f => f with { Tag = tag }); };
                panel.Children.Add(radio);
            }
        }

        var save = new Button { Name = "ExplorerSaveSearch", Content = Loc.Current["explorer.saveSearch"], IsEnabled = searchText.Trim().Length > 0 };
        save.Click += (_, _) => change(q => q.AddSearch(searchText));
        panel.Children.Add(save);
        return panel;
    }

    private static TextBlock Heading(string key, bool center = false) => new() { Text = Loc.Current[key], Classes = { "muted" }, Margin = new Thickness(0, Space("Space.2"), 0, 0), TextAlignment = center ? Avalonia.Media.TextAlignment.Center : Avalonia.Media.TextAlignment.Left };

    // A long label wraps inside the popup instead of running under the scroll bar.
    private static TextBlock Wrapped(string key) => new() { Text = Loc.Current[key], TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    private static double Space(string key) => Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out var value) && value is double d ? d : 8;
}
