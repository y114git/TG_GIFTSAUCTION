using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class TabHistoryTests
{
    private static readonly Func<string, string, bool> Any = (_, _) => true;

    private static TabInfo Show(EditorTabs tabs, string key, bool preview = true)
    {
        var tab = tabs.Open("resource", key, preview, _ => true).Tab;
        tabs.Visit(tab.Id, "resource", key);
        return tab;
    }

    [Fact]
    public void ABrowsingTabWalksItsOwnVisitsAndANewVisitDropsTheForwardPart()
    {
        var tabs = new EditorTabs();
        var tab = Show(tabs, "a");
        foreach (var key in new[] { "b", "c" }) tab = Show(tabs, key); // each preview replaces the last and carries its history
        Assert.True(tabs.CanGoBack(tab.Id));
        Assert.False(tabs.CanGoForward(tab.Id));
        Assert.Equal("b", tabs.Step(tab.Id, -1, Any)?.Key);
        Assert.Equal("a", tabs.Step(tab.Id, -1, Any)?.Key);
        Assert.Null(tabs.Step(tab.Id, -1, Any));
        Assert.Equal("b", tabs.Step(tab.Id, 1, Any)?.Key);
        Assert.Equal("b", tabs.Get(tab.Id)?.Key);
    }

    [Fact]
    public void AnotherTabKeepsItsOwnHistoryAndANormalNewTabStartsEmpty()
    {
        var tabs = new EditorTabs();
        Show(tabs, "a");
        var browsing = Show(tabs, "b");
        var other = Show(tabs, "z", preview: false);
        Assert.False(tabs.CanGoBack(other.Id));
        Assert.Null(tabs.Step(other.Id, -1, Any));
        Assert.True(tabs.CanGoBack(browsing.Id));
    }

    [Fact]
    public void ResourcesThatAreGoneOrOpenInAnotherTabAreSkipped()
    {
        var tabs = new EditorTabs();
        var tab = Show(tabs, "a");
        foreach (var key in new[] { "b", "c" }) tab = Show(tabs, key);
        Assert.Equal("a", tabs.Step(tab.Id, -1, (_, key) => key != "b")?.Key);
        Assert.Equal("b", tabs.Step(tab.Id, 1, Any)?.Key);
        Assert.Equal("c", tabs.Step(tab.Id, 1, Any)?.Key);
        Show(tabs, "a", preview: false); // a tab of its own now shows "a"
        Assert.Null(tabs.Step(tab.Id, -1, (_, key) => key != "b"));
    }

    [Fact]
    public void ReopenClosedTakesTheLastClosedThatStillExistsAndIsNotOpen()
    {
        var tabs = new EditorTabs();
        foreach (var key in new[] { "a", "b", "c" }) Show(tabs, key, preview: false);
        foreach (var key in new[] { "a", "b", "c" }) tabs.Remove(tabs.Find("resource", key)!.Id);
        Assert.Equal("b", tabs.PopClosed((_, key) => key != "c")?.Key);
        Show(tabs, "a", preview: false);
        Assert.Null(tabs.PopClosed(Any));
    }

    [Fact]
    public void ARepeatedRemoveOrAReplacedPreviewIsNotRemembered()
    {
        var tabs = new EditorTabs();
        var first = tabs.Open("resource", "a", true, _ => true).Tab;
        tabs.Open("resource", "b", true, _ => true);
        tabs.Remove(first.Id);
        Assert.Null(tabs.PopClosed(Any));
    }

    [Fact]
    public void ARenamedResourceKeepsItsPlaceInTheHistory()
    {
        var tabs = new EditorTabs();
        Show(tabs, "a");
        var tab = Show(tabs, "b");
        tabs.Rekey("a", "z");
        Assert.Equal("z", tabs.Step(tab.Id, -1, Any)?.Key);
    }
}
