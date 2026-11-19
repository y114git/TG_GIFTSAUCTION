using Avalonia.LogicalTree;
using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

public sealed class ExplorerEmptyTypesTests
{
    [Fact]
    public void TypesWithoutEntriesAreListedUntilTheFilterHidesThem()
    {
        using var data = TestData.Create(3);
        var model = new ExplorerModel(key => key);
        model.Load(data);
        bool IsType(ExplorerRow r) => r.Kind == ExplorerRowKind.Type && ResourceTypes.All.Any(t => t.Key == r.Key);
        var all = model.Rows.Where(IsType).ToList();
        Assert.Contains(all, r => r.Key == "objects");
        Assert.Contains(all, r => r.Count == 0);
        Assert.True(model.Quick.Filter.IsDefault);

        model.Quick = model.Quick with { Filter = model.Quick.Filter with { HideEmpty = true } };
        var shown = model.Rows.Where(IsType).ToList();
        Assert.All(shown, r => Assert.True(r.Count > 0, r.Key));
        Assert.True(all.Count > shown.Count);
        Assert.False(model.Quick.Filter.IsDefault);

        model.Quick = model.Quick with { Filter = model.Quick.Filter with { HideEmpty = false } };
        Assert.Equal(all.Count, model.Rows.Count(IsType));
        Assert.True(model.Quick.Filter.IsDefault);
    }

    [Theory]
    [InlineData("""{"Dock":"d","Tabs":[],"Quick":{"Recent":[],"Favourites":[],"Searches":[],"Tags":{},"Filter":{"ShowEmpty":true}}}""")]
    [InlineData("""{"Dock":"d","Tabs":[],"Quick":{"Recent":[],"Favourites":[],"Searches":[],"Tags":{},"Filter":{"ShowEmpty":false}}}""")]
    [InlineData("""{"Dock":"d","Tabs":[],"Quick":{"Recent":[],"Favourites":[],"Searches":[],"Tags":{},"Filter":{}}}""")]
    public void AFilterSavedBeforeTheOptionWasReversedShowsEmptyTypes(string saved)
    {
        // The old key is ignored: neither old value becomes an explicit hide.
        var state = System.Text.Json.JsonSerializer.Deserialize<ViewState>(saved);
        Assert.False(state!.Quick!.Filter.HideEmpty);
    }

    [Fact]
    public async Task TheCheckBoxUnderTheDataGroupOptionWritesTheFilter()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var quick = QuickAccess.Empty;
            var popup = ExplorerFilterPopup.Build(quick.Filter, [], "", change => quick = change(quick));
            var boxes = popup.Children.OfType<CheckBox>().ToList();
            var first = boxes.FindIndex(b => b.Name == "ExplorerGroupsFirst");
            Assert.True(first >= 0 && boxes[first + 1].Name == "ExplorerHideEmpty");
            var empty = boxes[first + 1];
            Assert.False(empty.IsChecked);
            Assert.Equal(Loc.Current["explorer.hideEmpty"], empty.Content);
            empty.IsChecked = true;
            Assert.True(quick.Filter.HideEmpty);
            Assert.True(ExplorerFilterPopup.Build(quick.Filter, [], "", _ => { }).Children.OfType<CheckBox>().Single(b => b.Name == "ExplorerHideEmpty").IsChecked); // a reopened popup shows the state in force
            return Task.CompletedTask;
        });
    }
    [Fact]
    public async Task ThePopupHoldsTheCodeEntryChoicesAndWritesTheSettings()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var settings = new G3MStudio.Core.Settings.AppSettings();
            var popup = ExplorerFilterPopup.Build(QuickAccess.Empty.Filter, [], "", _ => { }, settings, change => settings = change(settings));
            var anonymous = popup.GetLogicalDescendants().OfType<ComboBox>().Single(b => b.Name == "ExplorerAnonymousFunctions");
            Assert.Equal(1, anonymous.SelectedIndex); // gray out is the default
            anonymous.SelectedIndex = 2;
            Assert.Equal(G3MStudio.Core.Settings.EntryVisibility.Hide, settings.ExplorerAnonymousFunctions);
            Assert.DoesNotContain(ExplorerFilterPopup.Build(QuickAccess.Empty.Filter, [], "", _ => { }).GetLogicalDescendants(), c => c is ComboBox);
            return Task.CompletedTask;
        });
    }
}
