using Avalonia;
using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Explorer;

/// <summary>
/// The content of the Explorer filter popup: sort, groups first, the types to show and the tag to narrow to. Every control writes
/// the filter at once through <c>change</c>; the popup is built again each time it opens, so it shows the state in force.
/// </summary>
public static class ExplorerFilterPopup
{
    public static StackPanel Build(ExplorerFilter filter, IReadOnlyList<string> tags, string searchText, Action<Func<QuickAccess, QuickAccess>> change)
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

        panel.Children.Add(Heading("explorer.typesHeading"));
        foreach (var type in ResourceTypes.All.Where(t => !t.IsData))
        {
            var box = new CheckBox { Content = Loc.Current["explorer.type." + type.Key], IsChecked = !filter.Hides(type.Key), Tag = type.Key };
            box.IsCheckedChanged += (_, _) => Set(f => f.Hides(type.Key) == (box.IsChecked == true) ? f.Toggle(type.Key) : f);
            panel.Children.Add(box);
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

    private static TextBlock Heading(string key) => new() { Text = Loc.Current[key], Classes = { "muted" }, Margin = new Thickness(0, Space("Space.2"), 0, 0) };

    private static double Space(string key) => Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out var value) && value is double d ? d : 8;
}
