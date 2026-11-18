using Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using G3MStudio.Localization;

namespace G3MStudio.Docking;

/// <summary>
/// The panel header menu (the three dots) of the docking library has fixed English items; each time it opens, the items get the program language
/// and a "Move to" submenu is added, so a panel can go to another side dock without dragging.
/// </summary>
public static class ToolMenu
{
    // The library's item order: Float, Dock, Dock as tabbed document, Auto hide, Close.
    private static readonly string[] Keys =
    [
        "DocumentTabStripItemFloatString", "ToolChromeControlDockString", "ToolPinItemControlDockAsDocumentString",
        "ToolChromeControlAutoHideString", "DocumentTabStripItemCloseString",
    ];

    private static readonly (string Id, string Key)[] Sides = [("LeftDock", "dock.StudioToolMoveLeft"), ("RightDock", "dock.StudioToolMoveRight"), ("BottomDock", "dock.StudioToolMoveBottom")];

    /// <summary>Hooks the menu button of every panel header under <paramref name="control"/>.</summary>
    public static void Hook(Control control, StudioDock dock) =>
        control.AddHandler(Button.ClickEvent, (_, e) =>
        {
            if (e.Source is Button { Name: "PART_MenuButton", Flyout: MenuFlyout flyout } button) Prepare(flyout, dock, () => (button.DataContext as IDock)?.ActiveDockable);
        });

    internal static void Prepare(MenuFlyout flyout, StudioDock dock, Func<IDockable?> tool)
    {
        foreach (var stale in flyout.Items.OfType<MenuItem>().Where(m => m.Tag as string == "home").ToList()) flyout.Items.Remove(stale);
        var all = flyout.Items.OfType<MenuItem>().ToList();
        var items = all.Where(m => m.Tag as string != "move").ToList();
        if (items.Count < Keys.Length) return;
        foreach (var (item, key) in items.Zip(Keys)) item.Header = Loc.Current["dock." + key];
        if (all.FirstOrDefault(m => m.Tag as string == "move") is { } have)
        {
            // Opened before: only the language may have changed.
            have.Header = Loc.Current["dock.StudioToolMoveTo"];
            foreach (var (side, (_, key)) in have.Items.OfType<MenuItem>().Zip(Sides)) side.Header = Loc.Current[key];
            ShowHome(flyout, items[1], dock, tool);
            return;
        }

        var move = new MenuItem { Header = Loc.Current["dock.StudioToolMoveTo"], Tag = "move" };
        foreach (var (id, key) in Sides)
        {
            var side = new MenuItem { Header = Loc.Current[key] };
            side.Click += (_, _) =>
            {
                if (tool() is { Owner: IDock from } moving && dock.Find<IToolDock>(id) is { } to && to != from) dock.Factory.MoveDockable(from, to, moving, null);
            };
            move.Items.Add(side);
        }

        flyout.Items.Insert(Keys.Length - 1, move);
        ShowHome(flyout, items[1], dock, tool);
    }

    // The library's Dock item only unpins an auto-hidden panel and stays disabled for a floating one; a floating panel gets an item that docks it back.
    private static void ShowHome(MenuFlyout flyout, MenuItem libraryDock, StudioDock dock, Func<IDockable?> tool)
    {
        var moving = tool();
        IDockable? top = moving;
        while (top?.Owner is { } up) top = up;
        var floating = top is IRootDock && !ReferenceEquals(top, dock.Layout);
        libraryDock.IsVisible = !floating;
        if (!floating || moving is null) return;
        var home = new MenuItem { Header = libraryDock.Header, Tag = "home" };
        home.Click += (_, _) => dock.DockHome(moving);
        flyout.Items.Insert(flyout.Items.IndexOf(libraryDock) + 1, home);
    }
}
